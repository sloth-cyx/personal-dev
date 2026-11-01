using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

static class Sprites {
    public static Bitmap[,] Images = new Bitmap[3,3];
    public static string[] Names = { "橘白像素猫", "星语 · 银发少女", "薄荷史莱姆" };
    public static void Load() {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("characters.png"))
        using(var atlas=new Bitmap(stream)) {
            // The generated atlas has unequal row heights; use its actual visual boundaries.
            int[] ys={0,398,900,1254}; int[] xs={0,418,836,1254};
            for(int r=0;r<3;r++) for(int c=0;c<3;c++) {
                int minX=xs[c+1],minY=ys[r+1],maxX=xs[c],maxY=ys[r];
                int rowStart=r==2? (c==1?885:900):ys[r];
                for(int y=rowStart;y<ys[r+1];y++) for(int x=xs[c];x<xs[c+1];x++)
                    if(atlas.GetPixel(x,y).A>32) { minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y); }
                if(maxX<minX || maxY<minY) throw new InvalidDataException("角色素材为空");
                using(var cell=atlas.Clone(Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1),PixelFormat.Format32bppArgb)) Images[r,c]=MainSprite(cell);
            }
        }
    }
    // Locate the main connected sprite in each atlas cell; discard unrelated neighboring fragments.
    public static Bitmap MainSprite(Bitmap cell) {
        int w=cell.Width,h=cell.Height;var seen=new bool[w*h];var pixels=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)pixels[y*w+x]=cell.GetPixel(x,y);
        var largest=new List<int>();
        for(int start=0;start<pixels.Length;start++){if(seen[start]||pixels[start].A<8)continue;var component=new List<int>();var queue=new Queue<int>();queue.Enqueue(start);seen[start]=true;
            while(queue.Count>0){int p=queue.Dequeue();component.Add(p);int px=p%w,py=p/w;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int x=px+dx,y=py+dy;if(x<0||x>=w||y<0||y>=h)continue;int n=y*w+x;if(!seen[n]&&pixels[n].A>=8){seen[n]=true;queue.Enqueue(n);}}
            }if(component.Count>largest.Count)largest=component;
        }
        int l=w,t=h,r=0,b=0;foreach(int p in largest){l=Math.Min(l,p%w);r=Math.Max(r,p%w);t=Math.Min(t,p/w);b=Math.Max(b,p/w);}
        var result=new Bitmap(r-l+1,b-t+1,PixelFormat.Format32bppArgb);foreach(int p in largest)result.SetPixel(p%w-l,p/w-t,pixels[p]);return result;
    }
    public static Bitmap Render(int pet,int pose,int size,double phase,bool flip,bool held) {
        var canvas=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(canvas)) {
            g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceOver;
            g.InterpolationMode=pet==0 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=PixelOffsetMode.Half;
            var image=Images[pet,pose];var idle=Images[pet,0];
            double scale=(size-24.0)/Math.Max(idle.Width,idle.Height);
            scale=Math.Min(scale,(size-24.0)/Math.Max(image.Width,image.Height));
            double breath=Math.Sin(phase)*(pose==2 ? .008 : .018);
            double stretch=held ? .10 : breath;
            int w=(int)(image.Width*scale*(1-stretch*.4)),h=(int)(image.Height*scale*(1+stretch));
            int x=(size-w)/2,y=size-12-h;
            if(pose==1 && !held) y-=(int)(Math.Abs(Math.Sin(phase*2))*7);
            if(flip) {g.TranslateTransform(size,0);g.ScaleTransform(-1,1);}
            g.DrawImage(image,new Rectangle(x,y,w,h));
        }
        return canvas;
    }
}

class Preferences {
    public bool Persist=true;
    public int[] AnchorX={int.MinValue,int.MinValue,int.MinValue},AnchorY={0,0,0};public int Pet=1;public bool Roam=true,Top=true;public int[] Sizes={180,220,180},X={int.MinValue,int.MinValue,int.MinValue},Y={0,0,0};
    static string Folder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopCompanions-MotionPreview");}}
    public static Preferences Load() {
        var p=new Preferences();try {
            var lines=File.ReadAllLines(Path.Combine(Folder,"settings.txt"));
            foreach(var line in lines) {var v=line.Split('=');if(v.Length!=2)continue;int n;
                if(v[0]=="pet" && int.TryParse(v[1],out n))p.Pet=Math.Max(0,Math.Min(2,n));
                if(v[0]=="roam")p.Roam=v[1]=="1";if(v[0]=="top")p.Top=v[1]=="1";
                for(int i=0;i<3;i++) { if(v[0]=="size"+i && int.TryParse(v[1],out n))p.Sizes[i]=Math.Max(110,Math.Min(320,n));
                    if(v[0]=="ax"+i && int.TryParse(v[1],out n))p.AnchorX[i]=n;if(v[0]=="ay"+i && int.TryParse(v[1],out n))p.AnchorY[i]=n; if(v[0]=="x"+i && int.TryParse(v[1],out n))p.X[i]=n;if(v[0]=="y"+i && int.TryParse(v[1],out n))p.Y[i]=n; }
            }
        }catch(IOException){}catch(UnauthorizedAccessException){}return p;
    }
    public void Save() { if(!Persist)return;try {
        Directory.CreateDirectory(Folder);var lines=new List<string>{"pet="+Pet,"roam="+(Roam?"1":"0"),"top="+(Top?"1":"0")};
        for(int i=0;i<3;i++){lines.Add("size"+i+"="+Sizes[i]);lines.Add("ax"+i+"="+AnchorX[i]);lines.Add("ay"+i+"="+AnchorY[i]);lines.Add("x"+i+"="+X[i]);lines.Add("y"+i+"="+Y[i]);}
        File.WriteAllLines(Path.Combine(Folder,"settings.txt"),lines.ToArray());
    }catch(IOException){}catch(UnauthorizedAccessException){} }
}

class PetForm:Form {
    GirlMotion girl=new GirlMotion();System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();double lastTime;CompanionBehavior behavior=new CompanionBehavior(0);PoseTransition transition=new PoseTransition();Point anchor;RangeOverlay rangeOverlay;bool showPicker=true; Preferences prefs; NotifyIcon tray;ContextMenuStrip menu; System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    bool held,dragged,sleep,autoSleep;Point mouseOffset,press;int direction=1,ticks;double phase;
    Picker picker;ToolStripMenuItem roamItem,topItem,sleepItem,hideItem;
    public PetForm(Preferences p):this(p,true) {}
    public PetForm(Preferences p,bool pickerOnStart) {showPicker=pickerOnStart;
        Icon = new Icon(typeof(PetForm).Assembly.GetManifestResourceStream("AppIcon.ico"));
        prefs=p;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
        Text="Desktop Companions · 少女动作试验版 v0.3.1";TopMost=p.Top;Size=new Size(p.Sizes[p.Pet],p.Sizes[p.Pet]);
        SetSavedPosition();LoadAnchor();
        menu=new ContextMenuStrip();menu.Items.Add("选择角色…",null,delegate{OpenPicker();});
        var pets=new ToolStripMenuItem("快速切换");for(int i=0;i<3;i++){int index=i;pets.DropDownItems.Add(Sprites.Names[i],null,delegate{Switch(index);});}menu.Items.Add(pets);
        var sizes=new ToolStripMenuItem("角色大小");foreach(int s in new[]{130,180,220,280}){int value=s;sizes.DropDownItems.Add(s+" px",null,delegate{ResizePet(value);});}menu.Items.Add(sizes);
        roamItem=new ToolStripMenuItem("自动散步"){Checked=p.Roam,CheckOnClick=true};roamItem.Click+=delegate{prefs.Roam=roamItem.Checked;Save();};menu.Items.Add(roamItem);
        topItem=new ToolStripMenuItem("始终置顶"){Checked=p.Top,CheckOnClick=true};topItem.Click+=delegate{prefs.Top=topItem.Checked;TopMost=prefs.Top;Save();};menu.Items.Add(topItem);
        sleepItem=new ToolStripMenuItem("睡觉 / 唤醒");sleepItem.Click+=delegate{sleep=!sleep;autoSleep=false;behavior.Reset(clock.Elapsed.TotalSeconds);};menu.Items.Add(sleepItem);
        menu.Items.Add("以当前位置设为活动中心",null,delegate{SetAnchor();behavior.Reset(clock.Elapsed.TotalSeconds);ShowRange();});
        menu.Items.Add("显示活动范围（10秒）",null,delegate{ShowRange();});
        menu.Items.Add(new ToolStripMenuItem("陪伴节奏：1分钟挥手 · 3分钟散步 · 无操作5分钟睡眠"){Enabled=false});
        menu.Items.Add("找回宠物（移到主屏）",null,delegate{BringHome();});
        hideItem=new ToolStripMenuItem("隐藏宠物");hideItem.Click+=delegate{ToggleVisible();};menu.Items.Add(hideItem);
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,delegate{Close();});
        tray=new NotifyIcon {Icon=this.Icon,Text="桌面伙伴 · 右键管理 / 双击选角色",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=delegate{OpenPicker();};
        MouseDown+=Down;MouseMove+=MovePet;MouseUp+=Up;
        timer.Interval=p.Pet==1?33:40;timer.Tick+=delegate{Animate();};timer.Start();
        Shown+=delegate{PaintPet();if(showPicker)OpenPicker();};
        FormClosed+=delegate{timer.Stop();timer.Dispose();Save();tray.Visible=false;tray.Dispose();menu.Dispose();if(picker!=null)picker.Close();if(rangeOverlay!=null)rangeOverlay.Close();};
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;return p;}}
    public static Point Clamp(Point p,Size size,Rectangle area) {return new Point(Math.Max(area.Left,Math.Min(area.Right-size.Width,p.X)),Math.Max(area.Top,Math.Min(area.Bottom-size.Height,p.Y)));}
    void SetSavedPosition() {
        int i=prefs.Pet;var area=Screen.PrimaryScreen.WorkingArea;
        Point p=prefs.X[i]==int.MinValue?new Point(area.Right-Width-50,area.Bottom-Height):new Point(prefs.X[i],prefs.Y[i]);
        area=Screen.FromPoint(new Point(p.X+Width/2,p.Y+Height/2)).WorkingArea;Location=Clamp(p,Size,area);
    }
    void Save(){prefs.X[prefs.Pet]=Left;prefs.Y[prefs.Pet]=Top;prefs.Save();}
    public void Switch(int i) {Save();timer.Interval=i==1?33:40;prefs.Pet=i;Size=new Size(prefs.Sizes[i],prefs.Sizes[i]);SetSavedPosition();sleep=autoSleep=false;girl=new GirlMotion();transition=new PoseTransition();LoadAnchor();behavior.Reset(clock.Elapsed.TotalSeconds);lastTime=clock.Elapsed.TotalSeconds;Save();Show();hideItem.Text="隐藏宠物";PaintPet();}
    public void ResizePet(int size) {int bottom=Bottom;prefs.Sizes[prefs.Pet]=size;Size=new Size(size,size);Location=Clamp(new Point(Left,bottom-size),Size,Zone);Save();PaintPet();}
    public void BringHome(){var a=Screen.PrimaryScreen.WorkingArea;Location=new Point(a.Right-Width-40,a.Bottom-Height);SetAnchor();behavior.Reset(clock.Elapsed.TotalSeconds);Show();hideItem.Text="隐藏宠物";Save();PaintPet();}
    void ToggleVisible(){if(Visible){Hide();hideItem.Text="显示宠物";}else{behavior.Reset(clock.Elapsed.TotalSeconds);Show();hideItem.Text="隐藏宠物";PaintPet();}}
    public void OpenPicker(){if(picker==null || picker.IsDisposed)picker=new Picker(this,prefs);picker.Show();picker.Activate();}
    void Down(object sender,MouseEventArgs e) {
        if(e.Button==MouseButtons.Right){behavior.Reset(clock.Elapsed.TotalSeconds);menu.Show(Cursor.Position);return;}if(e.Button!=MouseButtons.Left)return;
        behavior.Reset(clock.Elapsed.TotalSeconds);held=true;dragged=false;mouseOffset=e.Location;press=Cursor.Position;Capture=true;sleep=autoSleep=false;
    }
    void MovePet(object sender,MouseEventArgs e){if(!held)return;var m=Cursor.Position;if(Math.Abs(m.X-press.X)+Math.Abs(m.Y-press.Y)>5)dragged=true;
        Location=Clamp(new Point(m.X-mouseOffset.X,m.Y-mouseOffset.Y),Size,Screen.FromPoint(m).WorkingArea);}
    void Up(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left||!held)return;held=false;Capture=false;if(dragged){SetAnchor();girl.Landing=1;behavior.Reset(clock.Elapsed.TotalSeconds);}else behavior.Greet(clock.Elapsed.TotalSeconds);Save();}
    Rectangle Zone {get{var area=Screen.FromPoint(anchor).WorkingArea;return ActivityArea.Around(anchor,Size,area);}}
    void LoadAnchor(){int i=prefs.Pet;if(prefs.AnchorX[i]==int.MinValue)SetAnchor();else{anchor=new Point(prefs.AnchorX[i],prefs.AnchorY[i]);var screen=Screen.FromPoint(anchor).WorkingArea;if(!screen.Contains(anchor))SetAnchor();else Location=Clamp(Location,Size,Zone);}}
    void SetAnchor(){anchor=new Point(Left+Width/2,Top+Height/2);prefs.AnchorX[prefs.Pet]=anchor.X;prefs.AnchorY[prefs.Pet]=anchor.Y;Location=Clamp(Location,Size,Zone);Save();}
    void ShowRange(){if(rangeOverlay!=null)rangeOverlay.Close();rangeOverlay=new RangeOverlay(Zone,Icon);rangeOverlay.Show();}
    void Animate(){
        double now=clock.Elapsed.TotalSeconds;double dt=now-lastTime;lastTime=now;if(!Visible)return;
        double frameDt=Math.Max(.001,Math.Min(.06,dt));ticks++;phase+=frameDt*3;
        bool blocked=held||menu.Visible||(picker!=null&&picker.Visible);
        behavior.Update(now,UserActivity.IdleSeconds,prefs.Roam,sleep,blocked);autoSleep=behavior.Sleeping&&!sleep;
        bool moving=behavior.IsWalking(now)&&!blocked;
        bool greeting=behavior.IsWaving(now)&&!blocked;
        if(held){girl.Speed=0;girl.PositionX=0;}
        int desired=held?2:Math.Abs(girl.Speed)>1&&!moving?1:behavior.Sleeping?3:greeting?2:moving?1:0;
        transition.Update(frameDt,desired);
        girl.Step(frameDt,direction,moving&&(prefs.Pet!=1||transition.Current==1),held,false,greeting||held);
        girl.Rest=behavior.Sleeping?1:0;
        if(!held){
            var zone=Zone;girl.PositionX+=girl.Speed*frameDt;int shift=(int)girl.PositionX;girl.PositionX-=shift;Left+=shift;
            if(Left<=zone.Left){Left=zone.Left;direction=1;girl.Speed=0;girl.PositionX=0;}
            if(Right>=zone.Right){Left=zone.Right-Width;direction=-1;girl.Speed=0;girl.PositionX=0;}
            Location=Clamp(Location,Size,zone);
        }
        PaintPet();
    }
    int Pose {get{return sleep||autoSleep?2:held||behavior.IsWaving(clock.Elapsed.TotalSeconds)?1:0;}}
    void PaintPet(){if(!IsHandleCreated||!Visible)return;using(var bitmap=prefs.Pet==1 ? GirlAnimation.RenderPose(transition.Current,Width,girl.Time,girl.Gait,direction,GirlAnimation.Blink(girl.Time),held,false,girl.Landing,transition.Compression) : Sprites.Render(prefs.Pet,Pose,Width,phase,direction<0,held))Native.Paint(Handle,bitmap,Location);}
}

class Picker:Form {
    public Picker(PetForm pet,Preferences prefs){Icon=pet.Icon;Text="桌面伙伴 · 选择角色";Size=new Size(750,410);MinimumSize=MaximumSize=Size;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(247,245,252);Font=new Font("Microsoft YaHei UI",10);MaximizeBox=false;
        var title=new Label {Text="今天想和谁一起？",Font=new Font(Font.FontFamily,18,FontStyle.Bold),Location=new Point(24,18),AutoSize=true};Controls.Add(title);
        string[] descriptions={"像素小猫 · 活泼好奇","银发少女 · 温柔陪伴","果冻史莱姆 · 弹跳软萌"};
        for(int i=0;i<3;i++){int index=i;var panel=new Panel {Location=new Point(20+i*238,65),Size=new Size(226,246),BackColor=Color.White};
            var picture=new PictureBox {Location=new Point(23,6),Size=new Size(180,180),Image=Sprites.Render(i,0,180,0,false,false),SizeMode=PictureBoxSizeMode.CenterImage};panel.Controls.Add(picture);
            var label=new Label {Text=descriptions[i],TextAlign=ContentAlignment.MiddleCenter,Location=new Point(0,183),Size=new Size(226,25)};panel.Controls.Add(label);
            var button=new Button {Text="选择 · "+Sprites.Names[i],Location=new Point(12,211),Size=new Size(202,30),FlatStyle=FlatStyle.Flat};button.Click+=delegate{pet.Switch(index);Hide();};panel.Controls.Add(button);Controls.Add(panel);
            FormClosed+=delegate{picture.Image.Dispose();};
        }
        Controls.Add(new Label {Text="左键点击互动 · 拖动搬家 · 放下固定活动中心 · 右键设置\n托盘图标可找回、隐藏或退出宠物；关闭本面板不会退出。",Location=new Point(25,322),Size=new Size(690,48),ForeColor=Color.FromArgb(90,85,110)});
    }
}

static class Native {
    [StructLayout(LayoutKind.Sequential)]struct POINT{public int x,y;public POINT(int a,int b){x=a;y=b;}}
    [StructLayout(LayoutKind.Sequential)]struct SIZE{public int cx,cy;public SIZE(int a,int b){cx=a;cy=b;}}
    [StructLayout(LayoutKind.Sequential,Pack=1)]struct BLEND{public byte op,flags,alpha,format;}
    [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr h,IntPtr dc);
    [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr dst,ref POINT p,ref SIZE size,IntPtr src,ref POINT source,uint key,ref BLEND blend,uint flags);
    [DllImport("user32.dll")]public static extern bool SetProcessDPIAware();
    public static void Paint(IntPtr window,Bitmap image,Point location){IntPtr screen=GetDC(IntPtr.Zero),dc=CreateCompatibleDC(screen),bmp=IntPtr.Zero,old=IntPtr.Zero;
        try{bmp=image.GetHbitmap(Color.FromArgb(0));old=SelectObject(dc,bmp);var point=new POINT(location.X,location.Y);var source=new POINT(0,0);var size=new SIZE(image.Width,image.Height);var blend=new BLEND {alpha=255,format=1};
            if(!UpdateLayeredWindow(window,screen,ref point,ref size,dc,ref source,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }finally{if(old!=IntPtr.Zero)SelectObject(dc,old);if(bmp!=IntPtr.Zero)DeleteObject(bmp);DeleteDC(dc);ReleaseDC(IntPtr.Zero,screen);}}
}

static class Program {
    [STAThread]static void Main(string[] args){Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try {Sprites.Load();GirlAnimation.Load();bool demo=args.Length>0 && args[0]=="--girl-demo";if(args.Length>0 && args[0]=="--self-test"){SelfTest();return;}
            bool created;using(var mutex=new System.Threading.Mutex(true,"Local\\DesktopCompanions.MotionPreview.SingleInstance",out created)){if(!created){MessageBox.Show("桌面伙伴或旧版试用程序正在运行，请先从其托盘菜单退出，再启动 v0.3.1。","桌面伙伴");return;}Application.Run(demo?new PetForm(new Preferences {Persist=false,Pet=1},false):new PetForm(Preferences.Load()));}
        }catch(Exception ex){MessageBox.Show("无法启动桌面伙伴："+ex.Message,"桌面伙伴",MessageBoxButtons.OK,MessageBoxIcon.Error);Environment.ExitCode=1;}
    }
    static void SelfTest(){int count=0;for(int r=0;r<3;r++)for(int c=0;c<3;c++)foreach(int size in new[]{130,180,220,280})using(var img=Sprites.Render(r,c,size,1.5,true,c==1)){
        if(img.Width!=size||img.GetPixel(0,0).A!=0)throw new Exception("透明画布检查失败");count++;}
        if(PetForm.Clamp(new Point(-100,9999),new Size(180,180),new Rectangle(0,0,1920,1080))!=new Point(0,900))throw new Exception("边界检查失败");count++;
        if(PetForm.Clamp(new Point(-9999,0),new Size(180,180),new Rectangle(-1920,0,1920,1080))!=new Point(-1920,0))throw new Exception("副屏检查失败");count++;
        using(var f=new PetForm(new Preferences {Persist=false})) {f.Show();Application.DoEvents();for(int i=0;i<3;i++){f.Switch(i);f.ResizePet(180);Application.DoEvents();}f.BringHome();Application.DoEvents();
            using(var picker=new Picker(f,new Preferences {Persist=false})){picker.Show();Application.DoEvents();using(var bmp=new Bitmap(picker.Width,picker.Height)){picker.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save("picker-preview.png");}picker.Close();}
            f.Close();}count++;
        using(var sheet=new Bitmap(660,660))using(var g=Graphics.FromImage(sheet)){g.Clear(Color.FromArgb(44,48,60));for(int r=0;r<3;r++)for(int c=0;c<3;c++)using(var sprite=Sprites.Render(r,c,220,0,false,false))g.DrawImageUnscaled(sprite,c*220,r*220);sheet.Save("sprite-preview.png");}
        File.WriteAllText("test-results.txt",count+" checks passed: 36 transparent renders, screen bounds, negative monitor coordinates, native window + character switching.\r\n");
    }
}









