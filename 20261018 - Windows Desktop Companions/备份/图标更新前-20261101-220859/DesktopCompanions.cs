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
    static Bitmap MainSprite(Bitmap cell) {
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
    public int Pet=0;public bool Roam=true,Top=true;public int[] Sizes={180,220,180},X={int.MinValue,int.MinValue,int.MinValue},Y={0,0,0};
    static string Folder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopCompanions");}}
    public static Preferences Load() {
        var p=new Preferences();try {
            var lines=File.ReadAllLines(Path.Combine(Folder,"settings.txt"));
            foreach(var line in lines) {var v=line.Split('=');if(v.Length!=2)continue;int n;
                if(v[0]=="pet" && int.TryParse(v[1],out n))p.Pet=Math.Max(0,Math.Min(2,n));
                if(v[0]=="roam")p.Roam=v[1]=="1";if(v[0]=="top")p.Top=v[1]=="1";
                for(int i=0;i<3;i++) { if(v[0]=="size"+i && int.TryParse(v[1],out n))p.Sizes[i]=Math.Max(110,Math.Min(320,n));
                    if(v[0]=="x"+i && int.TryParse(v[1],out n))p.X[i]=n;if(v[0]=="y"+i && int.TryParse(v[1],out n))p.Y[i]=n; }
            }
        }catch(IOException){}catch(UnauthorizedAccessException){}return p;
    }
    public void Save() { if(!Persist)return;try {
        Directory.CreateDirectory(Folder);var lines=new List<string>{"pet="+Pet,"roam="+(Roam?"1":"0"),"top="+(Top?"1":"0")};
        for(int i=0;i<3;i++){lines.Add("size"+i+"="+Sizes[i]);lines.Add("x"+i+"="+X[i]);lines.Add("y"+i+"="+Y[i]);}
        File.WriteAllLines(Path.Combine(Folder,"settings.txt"),lines.ToArray());
    }catch(IOException){}catch(UnauthorizedAccessException){} }
}

class PetForm:Form {
    Preferences prefs; NotifyIcon tray;ContextMenuStrip menu; System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    Random random=new Random();bool held,dragged,sleep,autoSleep;Point mouseOffset,press;int direction=1,ticks,actionUntil,decision=150;double phase,fallSpeed;bool falling;
    Picker picker;ToolStripMenuItem roamItem,topItem,sleepItem,hideItem;
    public PetForm(Preferences p) {
        prefs=p;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
        Text="Desktop Companions";TopMost=p.Top;Size=new Size(p.Sizes[p.Pet],p.Sizes[p.Pet]);
        SetSavedPosition();
        menu=new ContextMenuStrip();menu.Items.Add("选择角色…",null,delegate{OpenPicker();});
        var pets=new ToolStripMenuItem("快速切换");for(int i=0;i<3;i++){int index=i;pets.DropDownItems.Add(Sprites.Names[i],null,delegate{Switch(index);});}menu.Items.Add(pets);
        var sizes=new ToolStripMenuItem("角色大小");foreach(int s in new[]{130,180,220,280}){int value=s;sizes.DropDownItems.Add(s+" px",null,delegate{ResizePet(value);});}menu.Items.Add(sizes);
        roamItem=new ToolStripMenuItem("自动散步"){Checked=p.Roam,CheckOnClick=true};roamItem.Click+=delegate{prefs.Roam=roamItem.Checked;Save();};menu.Items.Add(roamItem);
        topItem=new ToolStripMenuItem("始终置顶"){Checked=p.Top,CheckOnClick=true};topItem.Click+=delegate{prefs.Top=topItem.Checked;TopMost=prefs.Top;Save();};menu.Items.Add(topItem);
        sleepItem=new ToolStripMenuItem("睡觉 / 唤醒");sleepItem.Click+=delegate{sleep=!sleep;autoSleep=false;actionUntil=0;};menu.Items.Add(sleepItem);
        menu.Items.Add("找回宠物（移到主屏）",null,delegate{BringHome();});
        hideItem=new ToolStripMenuItem("隐藏宠物");hideItem.Click+=delegate{ToggleVisible();};menu.Items.Add(hideItem);
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,delegate{Close();});
        tray=new NotifyIcon {Icon=SystemIcons.Information,Text="桌面伙伴 · 右键管理 / 双击选角色",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=delegate{OpenPicker();};
        MouseDown+=Down;MouseMove+=MovePet;MouseUp+=Up;
        timer.Interval=40;timer.Tick+=delegate{Animate();};timer.Start();
        Shown+=delegate{PaintPet();OpenPicker();};
        FormClosed+=delegate{timer.Stop();timer.Dispose();Save();tray.Visible=false;tray.Dispose();menu.Dispose();if(picker!=null)picker.Close();};
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
    public void Switch(int i) {Save();prefs.Pet=i;Size=new Size(prefs.Sizes[i],prefs.Sizes[i]);SetSavedPosition();sleep=autoSleep=falling=false;actionUntil=ticks+45;Save();Show();hideItem.Text="隐藏宠物";PaintPet();}
    public void ResizePet(int size) {int bottom=Bottom;prefs.Sizes[prefs.Pet]=size;Size=new Size(size,size);Location=Clamp(new Point(Left,bottom-size),Size,Screen.FromControl(this).WorkingArea);Save();PaintPet();}
    public void BringHome(){var a=Screen.PrimaryScreen.WorkingArea;Location=new Point(a.Right-Width-40,a.Bottom-Height);falling=false;Show();hideItem.Text="隐藏宠物";Save();PaintPet();}
    void ToggleVisible(){if(Visible){Hide();hideItem.Text="显示宠物";}else{Show();hideItem.Text="隐藏宠物";PaintPet();}}
    public void OpenPicker(){if(picker==null || picker.IsDisposed)picker=new Picker(this,prefs);picker.Show();picker.Activate();}
    void Down(object sender,MouseEventArgs e) {
        if(e.Button==MouseButtons.Right){menu.Show(Cursor.Position);return;}if(e.Button!=MouseButtons.Left)return;
        held=true;dragged=false;mouseOffset=e.Location;press=Cursor.Position;Capture=true;sleep=autoSleep=false;falling=false;
    }
    void MovePet(object sender,MouseEventArgs e){if(!held)return;var m=Cursor.Position;if(Math.Abs(m.X-press.X)+Math.Abs(m.Y-press.Y)>5)dragged=true;
        Location=Clamp(new Point(m.X-mouseOffset.X,m.Y-mouseOffset.Y),Size,Screen.FromPoint(m).WorkingArea);}
    void Up(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left||!held)return;held=false;Capture=false;actionUntil=ticks+45;falling=dragged;fallSpeed=0;Save();}
    void Animate(){if(!Visible)return;ticks++;phase+=.12;
        var area=Screen.FromControl(this).WorkingArea;
        if(!held && falling){fallSpeed=Math.Min(18,fallSpeed+1.2);Top=Math.Min(area.Bottom-Height,Top+(int)fallSpeed);if(Bottom>=area.Bottom){falling=false;actionUntil=ticks+15;Save();}}
        if(ticks>=decision && !held && !falling && !sleep){decision=ticks+random.Next(130,260);if(autoSleep)autoSleep=false;else if(random.Next(5)==0)autoSleep=true;direction=random.Next(2)==0?-1:1;}
        if(prefs.Roam && !sleep && !autoSleep && !held && !falling && ticks>actionUntil && ticks%350<180){Left+=direction*2;if(Left<area.Left){Left=area.Left;direction=1;}if(Right>area.Right){Left=area.Right-Width;direction=-1;}}
        if(!held)Location=Clamp(Location,Size,area);PaintPet();
    }
    int Pose {get{return sleep||autoSleep?2:held||ticks<actionUntil?1:0;}}
    void PaintPet(){if(!IsHandleCreated||!Visible)return;using(var bitmap=Sprites.Render(prefs.Pet,Pose,Width,phase,direction<0 && prefs.Pet!=1,held))Native.Paint(Handle,bitmap,Location);}
}

class Picker:Form {
    public Picker(PetForm pet,Preferences prefs){Text="桌面伙伴 · 选择角色";Size=new Size(750,410);MinimumSize=MaximumSize=Size;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(247,245,252);Font=new Font("Microsoft YaHei UI",10);MaximizeBox=false;
        var title=new Label {Text="今天想和谁一起？",Font=new Font(Font.FontFamily,18,FontStyle.Bold),Location=new Point(24,18),AutoSize=true};Controls.Add(title);
        string[] descriptions={"像素小猫 · 活泼好奇","银发少女 · 温柔陪伴","果冻史莱姆 · 弹跳软萌"};
        for(int i=0;i<3;i++){int index=i;var panel=new Panel {Location=new Point(20+i*238,65),Size=new Size(226,246),BackColor=Color.White};
            var picture=new PictureBox {Location=new Point(23,6),Size=new Size(180,180),Image=Sprites.Render(i,0,180,0,false,false),SizeMode=PictureBoxSizeMode.CenterImage};panel.Controls.Add(picture);
            var label=new Label {Text=descriptions[i],TextAlign=ContentAlignment.MiddleCenter,Location=new Point(0,183),Size=new Size(226,25)};panel.Controls.Add(label);
            var button=new Button {Text="选择 · "+Sprites.Names[i],Location=new Point(12,211),Size=new Size(202,30),FlatStyle=FlatStyle.Flat};button.Click+=delegate{pet.Switch(index);Hide();};panel.Controls.Add(button);Controls.Add(panel);
            FormClosed+=delegate{picture.Image.Dispose();};
        }
        Controls.Add(new Label {Text="左键点击互动 · 拖动搬家 · 松手落地 · 右键设置\n托盘图标可找回、隐藏或退出宠物；关闭本面板不会退出。",Location=new Point(25,322),Size=new Size(690,48),ForeColor=Color.FromArgb(90,85,110)});
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
        try {Sprites.Load();if(args.Length>0 && args[0]=="--self-test"){SelfTest();return;}
            bool created;using(var mutex=new System.Threading.Mutex(true,"Local\\DesktopCompanions.SingleInstance",out created)){if(!created){MessageBox.Show("桌面伙伴已经运行，请在系统托盘中找回它。","桌面伙伴");return;}Application.Run(new PetForm(Preferences.Load()));}
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

