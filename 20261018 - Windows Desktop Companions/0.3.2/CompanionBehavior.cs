using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class CompanionBehavior {
    public const double WaveInterval=60,WalkInterval=180,SleepAfter=300;
    public const double WaveDuration=6,WalkDuration=18;
    public double NextWave,NextWalk,WaveUntil,WalkUntil;
    public bool Sleeping,WasSleeping;
    public CompanionBehavior(double now){Reset(now);}
    public void Reset(double now){NextWave=now+WaveInterval;NextWalk=now+WalkInterval;WaveUntil=WalkUntil=0;}
    public void Greet(double now){Reset(now);WaveUntil=now+WaveDuration;}
    public void Update(double now,double inactive,bool roam,bool manualSleep,bool blocked){
        WasSleeping=Sleeping;Sleeping=manualSleep||inactive>=SleepAfter;
        if(Sleeping){WaveUntil=WalkUntil=0;return;}
        if(WasSleeping)Reset(now);
        if(blocked){WalkUntil=0;return;}
        if(!roam)WalkUntil=0;
        if(WalkUntil>0 && now>=WalkUntil){Reset(now);return;}
        if(roam && now>=NextWalk && WalkUntil==0){WalkUntil=now+WalkDuration;WaveUntil=0;return;}
        if(now>=NextWave && now>=WalkUntil){WaveUntil=now+WaveDuration;NextWave=now+WaveInterval;}
    }
    public bool IsWalking(double now){return !Sleeping&&now<WalkUntil;}
    public bool IsWaving(double now){return !Sleeping&&now<WaveUntil;}
}

// No cross-dissolve: exactly one opaque character pose is selected per frame.
class PoseTransition {
    public int Current,Target;
    public bool Active;
    public double Progress;
    bool deep;
    public double Compression {get{return Active?Math.Sin(Math.PI*Progress)*(deep?.17:.045):0;}}
    public void Update(double dt,int desired){
        if(!Active && desired!=Current){deep=Current==3||desired==3;Target=desired;Active=true;Progress=0;}
        if(!Active)return;
        Progress=Math.Min(1,Progress+Math.Max(0,dt)/.42);
        if(Progress>=.5)Current=Target;
        if(Progress>=1){Current=Target;Active=false;Progress=0;}
    }
}

static class ActivityArea {
    public static Rectangle Around(Point center,Size pet,Rectangle screen){
        double factor=Math.Sqrt(1.0/8);
        int w=Math.Min(screen.Width,Math.Max(pet.Width,(int)Math.Round(screen.Width*factor)));
        int h=Math.Min(screen.Height,Math.Max(pet.Height,(int)Math.Round(screen.Height*factor)));
        int x=Math.Max(screen.Left,Math.Min(screen.Right-w,center.X-w/2));
        int y=Math.Max(screen.Top,Math.Min(screen.Bottom-h,center.Y-h/2));
        return new Rectangle(x,y,w,h);
    }
}

static class UserActivity {
    [StructLayout(LayoutKind.Sequential)]struct LASTINPUTINFO {public uint cbSize,dwTime;}
    [DllImport("user32.dll")]static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    public static double IdleSeconds {get{
        var info=new LASTINPUTINFO {cbSize=(uint)Marshal.SizeOf(typeof(LASTINPUTINFO))};
        if(!GetLastInputInfo(ref info))return 0; // Failed queries must not cause sleep.
        return unchecked((uint)Environment.TickCount-info.dwTime)/1000.0;
    }}
}

class RangeOverlay:Form {
    Timer timeout=new Timer();
    public RangeOverlay(Rectangle area,Icon icon){Bounds=area;Icon=icon;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.Magenta;TransparencyKey=Color.Magenta;
        timeout.Interval=10000;timeout.Tick+=delegate{Close();};Shown+=delegate{timeout.Start();};FormClosed+=delegate{timeout.Dispose();};}
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80|0x20|0x08000000;return p;}}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Color.MediumPurple,3)){pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;e.Graphics.DrawRectangle(pen,2,2,Width-5,Height-5);}using(var font=new Font("Microsoft YaHei UI",10))e.Graphics.DrawString("固定活动范围 · 屏幕面积约 1/8",font,Brushes.MediumPurple,10,10);}
}
