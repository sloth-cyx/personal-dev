using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

// Cat: grounded four-foot gait. Slime: elastic hop with a fixed landing plane.
// Each render selects exactly one opaque sprite, never cross-fades silhouettes.
static class AnimalAnimation {
    static Bitmap[][] frames=new Bitmap[2][];
    static Bitmap[] sleeping=new Bitmap[2];
    static bool loaded;
    public static void Load(){
        if(loaded)return;
        for(int pet=0;pet<2;pet++){
            using(var stream=typeof(AnimalAnimation).Assembly.GetManifestResourceStream(pet==0?"cat-motion.png":"slime-motion.png"))
            using(var sheet=new Bitmap(stream)){
                var crops=new Bitmap[16];int extent=0;
                for(int i=0;i<16;i++){
                    using(var cell=sheet.Clone(new Rectangle(i%4*sheet.Width/4,i/4*sheet.Height/4,sheet.Width/4,sheet.Height/4),PixelFormat.Format32bppArgb))crops[i]=Sprites.MainSprite(cell);
                    extent=Math.Max(extent,Math.Max(crops[i].Width,crops[i].Height));
                }
                frames[pet]=new Bitmap[16];
                for(int i=0;i<16;i++)using(var crop=crops[i])frames[pet][i]=Register(crop,232.0/extent,pet==0);
                var original=Sprites.Images[pet==0?0:2,2];sleeping[pet]=Register(original,232.0/Math.Max(original.Width,original.Height),pet==0);
            }
        }loaded=true;
    }
    static Bitmap Register(Bitmap source,double scale,bool pixel){
        // Register on the weight-bearing lower body so a curling tail does not
        // drag the torso sideways. Keep a common scale across every atlas cell.
        double sum=0,count=0;
        for(int y=(int)(source.Height*.72);y<source.Height;y++)for(int x=0;x<source.Width;x++)if(source.GetPixel(x,y).A>128){sum+=x;count++;}
        double center=sum/Math.Max(1,count);var result=new Bitmap(256,256,PixelFormat.Format32bppArgb);
        double left=Math.Max(8,Math.Min(248-source.Width*scale,128-center*scale));
        using(var g=Graphics.FromImage(result)){
            g.InterpolationMode=pixel?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;
            g.DrawImage(source,new RectangleF((float)left,(float)(244-source.Height*scale),(float)(source.Width*scale),(float)(source.Height*scale)));
        }return result;
    }
    static double Cycle(double phase){return phase-Math.Floor(phase);}
    public static int FrameIndex(int selected,double time,double gait,double actionTime){
        if(selected==1)return 4+(int)(Cycle(gait/(Math.PI*2))*8);
        if(selected==2){if(actionTime<.4)return 12;if(actionTime>5.5)return 15;return 13+((int)(actionTime*3)%2);}
        double t=time%5.7;return t<5.12?0:t<5.24?1:t<5.42?2:3;
    }
    public static double HopHeight(double gait){double p=Cycle(gait/(Math.PI*2));return p>.125&&p<.75?Math.Sin((p-.125)/.625*Math.PI)*14:0;}
    public static Bitmap Render(int pet,int selected,int size,double time,double gait,int direction,bool held,double landing,double compression,double actionTime){
        Load();int species=pet==0?0:1;var canvas=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        var source=selected==3?sleeping[species]:frames[species][FrameIndex(selected,time,gait,actionTime)];
        double scale=(size-16.0)/256;float baseline=(float)(8+244*scale);
        using(var g=Graphics.FromImage(canvas)){
            g.InterpolationMode=pet==0?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;
            double breathe=selected==3?Math.Sin(time*1.35)*.006:pet==2&&selected==0?Math.Sin(time*2.1)*.017:0;
            double squash=compression+landing*.055+breathe;
            g.TranslateTransform(size/2f,baseline);g.ScaleTransform((float)(1+squash*.35),(float)(1-squash));g.TranslateTransform(-size/2f,-baseline);
            if(direction<0&&selected==1){g.TranslateTransform(size,0);g.ScaleTransform(-1,1);}
            double lift=pet==2&&selected==1?HopHeight(gait)*scale:0;
            if(held)lift+=Math.Sin(time*5)*1.2;
            if(pet==0&&selected==0&&!held){using(var tail=TailAndBreath(source,time))g.DrawImage(tail,new RectangleF(8,(float)(8-lift),size-16,size-16));}
            else g.DrawImage(source,new RectangleF(8,(float)(8-lift),size-16,size-16));
        }return canvas;
    }
    static Bitmap TailAndBreath(Bitmap source,double time){
        const int size=256;var rect=new Rectangle(0,0,size,size);var input=new byte[size*size*4];var output=new byte[input.Length];
        var data=source.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(data.Scan0,input,0,input.Length);}finally{source.UnlockBits(data);}
        for(int y=0;y<size;y++){
            double ny=y/256.0;double breath=(1-GirlAnimation.Smooth(.65,.86,ny))*Math.Sin(time*2)*.65;
            double tail=Math.Sin(time*1.7+ny*3)*3.2*(1-GirlAnimation.Smooth(.67,.82,ny));
            for(int x=0;x<size;x++){
                double mask=1-GirlAnimation.Smooth(.23,.44,x/256.0);
                int sx=(int)Math.Round(x-tail*mask),sy=(int)Math.Round(y-breath);
                if(sx<0||sx>=size||sy<0||sy>=size)continue;
                int from=(sy*size+sx)*4,to=(y*size+x)*4;
                output[to]=input[from];output[to+1]=input[from+1];output[to+2]=input[from+2];output[to+3]=input[from+3];
            }
        }
        var result=new Bitmap(size,size,PixelFormat.Format32bppArgb);data=result.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(output,0,data.Scan0,output.Length);}finally{result.UnlockBits(data);}return result;
    }
}

// Finish the current hop before switching to a grounded idle/sleep pose.
class HopLanding {
    double target=double.NaN;
    public bool Prepare(double gait,bool moving,int current,bool held){
        if(moving||current!=1||held){target=double.NaN;return false;}
        if(double.IsNaN(target))target=Math.Ceiling((gait-.000001)/(Math.PI*2))*Math.PI*2;
        return target>gait+.00001;
    }
    public double Apply(double before,double after,double dt){return double.IsNaN(target)?after:Math.Min(target,before+Math.PI*2*Math.Max(0,dt)*1.7);}
}
