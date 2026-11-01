using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
class TransitionPreview {
 [STAThread]static void Main(){Sprites.Load();GirlAnimation.Load();var gif=new GifBitmapEncoder();var transition=new PoseTransition();
 for(int i=0;i<140;i++){
  double t=i*.05;int desired=t<1?0:t<2.5?2:t<3.2?0:t<4.5?1:t<5?0:t<6.3?3:0;transition.Update(.05,desired);
  using(var canvas=new Bitmap(480,330))using(var g=Graphics.FromImage(canvas))using(var font=new Font("Microsoft YaHei UI",12)){
   g.Clear(Color.FromArgb(44,48,60));g.DrawString("v0.3 姿态衔接 · 加速演示",font,Brushes.White,18,12);
   using(var frame=GirlAnimation.RenderPose(transition.Current,280,t,t*Math.PI*2,1,GirlAnimation.Blink(t),false,false,0,transition.Compression))g.DrawImageUnscaled(frame,100,42);
   using(var stream=new MemoryStream()){
    canvas.Save(stream,ImageFormat.Png);stream.Position=0;var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
    var metadata=new BitmapMetadata("gif");metadata.SetQuery("/grctlext/Delay",(ushort)5);metadata.SetQuery("/grctlext/Disposal",(byte)2);
    if(i==0){metadata.SetQuery("/appext/application",System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));metadata.SetQuery("/appext/data",new byte[]{3,1,0,0,0});}
    gif.Frames.Add(BitmapFrame.Create(decoder.Frames[0],null,metadata,null));
   }
  }
 }
 using(var file=File.Create("姿态衔接-v0.3.gif"))gif.Save(file);
 }
}
