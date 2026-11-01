using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

// Coordinates are normalized to the original artwork. Motion is applied to
// separate head, hair, skirt and leg regions, rather than scaling the whole card.
static class GirlAnimation {
    static byte[][] open=new byte[3][],closed=new byte[3][];
    static int[] widths=new int[3],heights=new int[3];
    static Bitmap[] walking=new Bitmap[8];
    public static double Smooth(double a,double b,double t) {t=Math.Max(0,Math.Min(1,(t-a)/(b-a)));return t*t*(3-2*t);}
    public static void Load(){for(int p=0;p<3;p++){
        var image=Sprites.Images[1,p];widths[p]=image.Width;heights[p]=image.Height;open[p]=Read(image);
        using(var blink=(Bitmap)image.Clone()){
            if(p<2)using(var g=Graphics.FromImage(blink)){
                g.SmoothingMode=SmoothingMode.AntiAlias;
                // Patch only the two eye interiors, keeping brows, fringe and blush.
                float offset=p==1?17:0;
                Eye(g,77+offset,120,38,32);Eye(g,145+offset,105,36,33);
            }
            closed[p]=Read(blink);
        }
    }
        using(var stream=typeof(GirlAnimation).Assembly.GetManifestResourceStream("girl-walk.png"))
        using(var sheet=new Bitmap(stream)){
            var crops=new Bitmap[8];int maxHeight=0;
            for(int i=0;i<8;i++){
                int x=i%4*sheet.Width/4,y=i/4*sheet.Height/2;
                using(var cell=sheet.Clone(new Rectangle(x,y,sheet.Width/4,sheet.Height/2),PixelFormat.Format32bppArgb))crops[i]=Sprites.MainSprite(cell);
                maxHeight=Math.Max(maxHeight,crops[i].Height);
            }
            for(int i=0;i<8;i++)using(var crop=crops[i]){
                // Register the head, rather than each frame's changing leg extent.
                double sum=0,count=0;for(int y=0;y<crop.Height*.26;y++)for(int x=0;x<crop.Width;x++)if(crop.GetPixel(x,y).A>128){sum+=x;count++;}
                double center=sum/Math.Max(1,count),scale=491.0/maxHeight;
                walking[i]=new Bitmap(430,539,PixelFormat.Format32bppArgb);
                using(var g=Graphics.FromImage(walking[i])){g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.DrawImage(crop,new RectangleF((float)(215-center*scale),(float)(515-crop.Height*scale),(float)(crop.Width*scale),(float)(crop.Height*scale)));}
            }
        }
    }
    static void Eye(Graphics g,float x,float y,float w,float h){
        using(var skin=new SolidBrush(Color.FromArgb(255,244,219,213)))
        using(var lash=new Pen(Color.FromArgb(255,73,43,62),3.2f)){
            g.FillEllipse(skin,x-6,y-7,w+12,h+14);
            g.DrawBezier(lash,x,y+h*.48f,x+w*.25f,y+h*.85f,x+w*.70f,y+h*.79f,x+w,y+h*.35f);
            g.DrawLine(lash,x+2,y+h*.52f,x-3,y+h*.38f);
        }
    }
    static byte[] Read(Bitmap b){var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var bytes=new byte[b.Width*b.Height*4];
        try{Marshal.Copy(d.Scan0,bytes,0,bytes.Length);}finally{b.UnlockBits(d);}return bytes;}
    public static double Blink(double time){double t=time%5.37;return Smooth(4.92,5.00,t)*(1-Smooth(5.07,5.20,t));}
    public static Bitmap Render(int pose,int size,double time,double gait,double walk,double facing,double blink,double greet,bool held,bool falling,double landing,double rest=0){
        int selected=pose==2||rest>=.5?3:greet>=.5?2:walk>=.25?1:0;
        return RenderPose(selected,size,time,gait,facing,blink,held,falling,landing,0);
    }
    public static Bitmap RenderPose(int selected,int size,double time,double gait,double facing,double blink,bool held,bool falling,double landing,double compression){
        var output=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(output)){
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;
            float baseline=size-7-(float)(24*(size-30.0)/491);
            g.TranslateTransform(size/2f,baseline);g.ScaleTransform((float)(1+compression*.18),(float)(1-compression));g.TranslateTransform(-size/2f,-baseline);
            if(selected==1){int index=((int)Math.Floor(gait/(Math.PI*2)*8)%8+8)%8;
                if(facing<0){g.TranslateTransform(size,0);g.ScaleTransform(-1,1);}Draw(g,walking[index],size,1);
            }else using(var frame=Warp(selected==3?2:selected==2?1:0,time,gait,0,facing,blink,held,falling,landing))Draw(g,frame,size,1);
        }return output;
    }
    static void Draw(Graphics g,Bitmap frame,int size,double alpha){
        double scale=(size-30.0)/491;int w=(int)(frame.Width*scale),h=(int)(frame.Height*scale);
        using(var attributes=new ImageAttributes()){
            var matrix=new ColorMatrix();matrix.Matrix33=(float)alpha;attributes.SetColorMatrix(matrix);
            g.DrawImage(frame,new Rectangle((size-w)/2,size-7-h,w,h),0,0,frame.Width,frame.Height,GraphicsUnit.Pixel,attributes);
        }
    }
    static Bitmap Warp(int pose,double time,double gait,double walk,double facing,double blink,bool held,bool falling,double landing,double rest=0){
        int w=widths[pose],h=heights[pose],pad=24,ow=w+pad*2,oh=h+pad*2;
        var bytes=new byte[ow*oh*4];var source=open[pose];var shut=closed[pose];
        double step=Math.Sin(gait),other=Math.Sin(gait+Math.PI);
        double bob=walk*(1-Math.Cos(gait*2))*2.6;
        double breathe=Math.Sin(time*2.05),sway=Math.Sin(time*.94);
        for(int y=0;y<oh;y++){
            double ny=(y-pad)/(double)h;
            double head=1-Smooth(.30,.48,ny),legs=Smooth(.69,.88,ny),skirt=Smooth(.47,.73,ny)*(1-Smooth(.78,.87,ny));
            double bodyX=(sway*3.6+facing*2.8)*head+walk*step*1.1;
            double bodyY=bob-(1-ny)*breathe*2.6;
            if(held){bodyX+=Math.Sin(time*5)*3*(1-head);bodyY-=(1-ny)*5;}
            if(falling)bodyY-=(1-ny)*4;
            bodyY+=landing*(1-ny)*8;
            for(int x=0;x<ow;x++){
                double nx=(x-pad)/(double)w;
                double leg=nx<.50?step:other;
                double hair=(1-Smooth(.17,.32,ny))*0 + Smooth(.18,.30,ny)*(1-Smooth(.41,.49,ny));
                double edge=Smooth(.15,.42,Math.Abs(nx-.5));
                double dx=bodyX+hair*edge*Math.Sin(time*2.3+ny*8)*(3.0+walk*2.2);
                dx+=skirt*Math.Sin(gait+ny*6)*walk*3.1;
                dx+=legs*leg*walk*5.5;
                // Swing foot lifts; the stance leg remains on the baseline.
                double dy=bodyY-legs*Math.Max(0,leg)*walk*11;
                if(pose==1){double hand=Smooth(.64,.84,nx)*(1-Smooth(.51,.63,ny))*Smooth(.21,.32,ny);dx+=hand*Math.Sin(time*12)*4;}
                double sx=x-pad-dx,sy=y-pad-dy;
                int ix=(int)Math.Floor(sx),iy=(int)Math.Floor(sy);
                if(ix<0||iy<0||ix>=w-1||iy>=h-1)continue;
                double fx=sx-ix,fy=sy-iy;int dst=(y*ow+x)*4;
                // Interpolate premultiplied color to avoid dark transparent fringes.
                double a=0,b=0,gg=0,r=0;
                for(int q=0;q<4;q++){
                    int index=((iy+q/2)*w+ix+q%2)*4;double weight=(q%2==0?1-fx:fx)*(q<2?1-fy:fy);
                    double aa=(source[index+3]*(1-blink)+shut[index+3]*blink)/255.0;
                    a+=aa*weight;b+=(source[index]*(1-blink)+shut[index]*blink)*aa*weight;
                    gg+=(source[index+1]*(1-blink)+shut[index+1]*blink)*aa*weight;r+=(source[index+2]*(1-blink)+shut[index+2]*blink)*aa*weight;
                }
                if(a>.001){bytes[dst]=(byte)(b/a);bytes[dst+1]=(byte)(gg/a);bytes[dst+2]=(byte)(r/a);bytes[dst+3]=(byte)(a*255);}
            }
        }
        var result=new Bitmap(ow,oh,PixelFormat.Format32bppArgb);var data=result.LockBits(new Rectangle(0,0,ow,oh),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try{Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{result.UnlockBits(data);}return result;
    }
}

class GirlMotion {
    public double Time,Gait,Speed,Walk,Facing=1,Greeting,Landing,Rest;
    public double TargetGreeting,PositionX;
    public bool Walking;
    public void Step(double dt,int direction,bool moving,bool held,bool falling,bool greet){
        dt=Math.Max(0,Math.Min(.06,dt));Time+=dt;
        double target=moving?direction*43:0;
        Speed+=(target-Speed)*(1-Math.Exp(-dt*(moving?4.2:7)));
        if(held||falling)Speed=0;
        if(Math.Abs(Speed)<.15)Speed=0;
        Walk+=(Math.Min(1,Math.Abs(Speed)/34)-Walk)*(1-Math.Exp(-dt*9));
        Gait+=Math.Abs(Speed)*dt*Math.PI/18;
        Facing+=(direction-Facing)*(1-Math.Exp(-dt*4));
        TargetGreeting=greet?1:0;Greeting+=(TargetGreeting-Greeting)*(1-Math.Exp(-dt*13));
        Landing*=Math.Exp(-dt*12);Walking=moving;
    }
}



