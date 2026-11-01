using System;
using System.IO;
using System.Drawing;
class GroundedTests {
 [STAThread]static void Main(){Sprites.Load();GirlAnimation.Load();int checks=0;
  foreach(int pose in new[]{0,2})foreach(int size in new[]{130,180,220,280,320}){
   using(var baseline=GirlAnimation.RenderPose(pose,size,0,0,1,0,false,false,0,0)){
    int floor=-1;for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(baseline.GetPixel(x,y).A>128)floor=Math.Max(floor,y);
    for(int i=1;i<12;i++)using(var frame=GirlAnimation.RenderPose(pose,size,i*.47,0,1,i%3==0?1:0,false,false,0,0)){
     for(int y=(int)(size*.87);y<size;y++)for(int x=0;x<size;x++)if(frame.GetPixel(x,y)!=baseline.GetPixel(x,y))throw new Exception("feet shifted during idle/wave/blink");checks++;
     int current=-1;for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(frame.GetPixel(x,y).A>128)current=Math.Max(current,y);
     if(current!=floor)throw new Exception("ground plane moved");checks++;
    }
   }
  }
  using(var sheet=new Bitmap(1120,560))using(var g=Graphics.FromImage(sheet)){
   g.Clear(Color.FromArgb(44,48,60));
   for(int i=0;i<8;i++)using(var frame=GirlAnimation.RenderPose(i%4<2?0:2,280,i*.9,0,1,i%2,false,false,0,0)){
    int x=i%4*280,y=i/4*280;g.DrawLine(Pens.DimGray,x,y+258,x+280,y+258);g.DrawImageUnscaled(frame,x,y);
   }sheet.Save("standing-preview.png");
  }
  File.WriteAllText("grounded-test-results.txt",checks+" planted-feet and ground-plane checks passed across idle, wave, blink and five sizes.\r\n");Console.WriteLine(checks+" grounded checks passed");
 }
}
