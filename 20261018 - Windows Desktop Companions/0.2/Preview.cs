using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
class Preview {
 [STAThread]static void Main(){Sprites.Load();GirlAnimation.Load();var gif=new GifBitmapEncoder();var motion=new GirlMotion();double position=0,oldPosition=0;
 for(int i=0;i<160;i++){
  double t=i*.05;bool walking=t>=2&&t<6;motion.Step(.05,1,walking,false,false,t>=7);position+=motion.Speed*.05;if(walking)oldPosition+=43*.05;
  using(var canvas=new Bitmap(900,340))using(var g=Graphics.FromImage(canvas))using(var font=new Font("Microsoft YaHei UI",13)){
   g.Clear(Color.FromArgb(44,48,60));g.DrawString("原版：静态姿势平移",font,Brushes.White,25,18);g.DrawString("v0.2：待机 → 散步 → 停步 → 招手",font,Brushes.White,465,18);
   using(var old=Sprites.Render(1,0,260,t*3,false,false))g.DrawImageUnscaled(old,10+(int)oldPosition,65);
   using(var frame=GirlAnimation.Render(0,260,motion.Time,motion.Gait,motion.Walk,motion.Facing,GirlAnimation.Blink(motion.Time),motion.Greeting,false,false,0))g.DrawImageUnscaled(frame,460+(int)position,65);
   g.DrawLine(Pens.DimGray,450,0,450,340);
   using(var stream=new MemoryStream()){
    canvas.Save(stream,ImageFormat.Png);stream.Position=0;var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
    var metadata=new BitmapMetadata("gif");metadata.SetQuery("/grctlext/Delay",(ushort)5);metadata.SetQuery("/grctlext/Disposal",(byte)2);
    if(i==0){metadata.SetQuery("/appext/application",System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));metadata.SetQuery("/appext/data",new byte[]{3,1,0,0,0});}
    gif.Frames.Add(BitmapFrame.Create(decoder.Frames[0],null,metadata,null));
   }
  }
 }
 using(var file=File.Create("少女动作对比.gif"))gif.Save(file);
 using(var sheet=new Bitmap(1100,440))using(var g=Graphics.FromImage(sheet)){
  g.Clear(Color.FromArgb(44,48,60));for(int i=0;i<10;i++)using(var frame=GirlAnimation.Render(0,220,i*.4,0,0,1,i==2?1:0,i>=4&&i<7?1:0,false,false,0,i>=7?1:0))g.DrawImageUnscaled(frame,(i%5)*220,i/5*220);sheet.Save("idle-preview.png");
 }
 }
}
