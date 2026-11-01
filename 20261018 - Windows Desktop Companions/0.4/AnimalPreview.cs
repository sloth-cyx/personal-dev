using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
class AnimalPreview {
 [STAThread]static void Main(){Sprites.Load();GirlAnimation.Load();AnimalAnimation.Load();var gif=new GifBitmapEncoder();var transitions=new[]{new PoseTransition(),new PoseTransition()};var motions=new[]{new GirlMotion(),new GirlMotion()};var xs=new[]{70,70};var directions=new[]{1,1};var previous=new int[2];var poseStarts=new double[2];var landing=new HopLanding();
  for(int i=0;i<180;i++){
   double t=i*.05;int desired=t<2?0:t<4?2:t<7?1:t<8?0:3;
   using(var canvas=new Bitmap(760,300))using(var g=Graphics.FromImage(canvas))using(var font=new Font("Microsoft YaHei UI",12)){
    g.Clear(Color.FromArgb(44,48,60));g.DrawString("橘猫 · 待机 / 抬爪 / 四足散步 / 睡眠",font,Brushes.White,12,12);g.DrawString("史莱姆 · 呼吸 / 招呼 / 弹跳 / 睡眠",font,Brushes.White,392,12);
    g.DrawLine(Pens.DimGray,380,0,380,300);
    for(int k=0;k<2;k++){
     var m=motions[k];var tr=transitions[k];bool pending=k==1&&landing.Prepare(m.Gait,desired==1,tr.Current,false);
     int target=pending||Math.Abs(m.Speed)>1&&desired!=1?1:desired;tr.Update(.05,target);if(previous[k]!=tr.Current){previous[k]=tr.Current;poseStarts[k]=t;}
     double before=m.Gait;m.Step(.05,directions[k],desired==1&&tr.Current==1,false,false,false);if(k==1)m.Gait=landing.Apply(before,m.Gait,.05);xs[k]=m.AdvanceHorizontal(xs[k],0,140,.05,ref directions[k]);
     using(var frame=AnimalAnimation.Render(k==0?0:2,tr.Current,220,t<2?4.4+t:t,m.Gait,directions[k],false,0,tr.Compression,t-poseStarts[k]))g.DrawImageUnscaled(frame,k*380+xs[k],62);
    }
    using(var stream=new MemoryStream()){
     canvas.Save(stream,ImageFormat.Png);stream.Position=0;var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
     var metadata=new BitmapMetadata("gif");metadata.SetQuery("/grctlext/Delay",(ushort)5);metadata.SetQuery("/grctlext/Disposal",(byte)2);
     if(i==0){metadata.SetQuery("/appext/application",System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));metadata.SetQuery("/appext/data",new byte[]{3,1,0,0,0});}
     gif.Frames.Add(BitmapFrame.Create(decoder.Frames[0],null,metadata,null));
    }
   }
  }
  using(var file=File.Create("橘猫与史莱姆-v0.4.gif"))gif.Save(file);
 }
}
