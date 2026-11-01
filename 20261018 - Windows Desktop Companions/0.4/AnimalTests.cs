using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
class AnimalTests {
 static int checks;
 static void Check(bool yes,string name){if(!yes)throw new Exception(name);checks++;}
 static long Hash(Bitmap b){long h=17;unchecked{for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)h=h*31+b.GetPixel(x,y).ToArgb();}return h;}
 static int Floor(Bitmap b){int floor=-1;for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>128)floor=y;return floor;}
 [STAThread]static void Main(){Sprites.Load();GirlAnimation.Load();AnimalAnimation.Load();
  foreach(int pet in new[]{0,2})foreach(int size in new[]{110,130,180,220,280,320})foreach(int pose in new[]{0,1,2,3})foreach(int direction in new[]{-1,1}){
   using(var b=AnimalAnimation.Render(pet,pose,size,2,2,direction,false,0,0,2)){
    Check(b.Width==size&&b.Height==size,"dimensions");Check(b.GetPixel(0,0).A==0&&b.GetPixel(size-1,size-1).A==0,"transparent corners");Check(Floor(b)>0&&Floor(b)<size-1,"visible and bottom margin");
   }
  }
  foreach(int pet in new[]{0,2}){
   var hashes=new HashSet<long>();int minFloor=999,maxFloor=-1;
   for(int i=0;i<8;i++){
    double gait=i*Math.PI/4;
    Check(AnimalAnimation.FrameIndex(1,0,gait,0)==4+i,"distance-linked frame order");
    using(var b=AnimalAnimation.Render(pet,1,220,0,gait,1,false,0,0,0)){
     hashes.Add(Hash(b));int floor=Floor(b);minFloor=Math.Min(minFloor,floor);maxFloor=Math.Max(maxFloor,floor);
    }
   }
   Check(hashes.Count==8,"eight distinct walk/hop frames");
   if(pet==0)Check(maxFloor-minFloor<=2,"cat planted contact plane");
   else Check(maxFloor-minFloor>=6,"slime actually leaves ground");
   using(var sheet=new Bitmap(1100,660))using(var g=Graphics.FromImage(sheet)){
    g.Clear(Color.FromArgb(44,48,60));for(int i=0;i<15;i++){
     int pose=i<4?0:i<12?1:i==14?3:2;double time=i<4?new[]{0,5.17,5.3,5.5}[i]:i*.15;
     double gait=i>=4&&i<12?(i-4)*Math.PI/4:0;
     using(var b=AnimalAnimation.Render(pet,pose,220,time,gait,1,false,0,0,2))g.DrawImageUnscaled(b,i%5*220,i/5*220);
    }sheet.Save(pet==0?"cat-preview.png":"slime-preview.png");
   }
  }
  Check(AnimalAnimation.HopHeight(0)==0&&AnimalAnimation.HopHeight(Math.PI*1.5)==0,"slime launch and landing phases");
  Check(AnimalAnimation.HopHeight(Math.PI*.875)>13,"slime apex");
  foreach(double phase in new[]{.01,.22,.47,.74,.99}){
   var landing=new HopLanding();double gait=phase*Math.PI*2;
   for(int i=0;i<30;i++){landing.Prepare(gait,false,1,false);double next=landing.Apply(gait,gait+.01,.033);Check(next>=gait&&next<=Math.PI*2,"finish hop without overshoot");gait=next;}
   Check(Math.Abs(gait-Math.PI*2)<.00001&&AnimalAnimation.HopHeight(gait)==0,"stop on ground before idle");
   Check(!landing.Prepare(gait,true,1,false)&&landing.Apply(gait,gait+.1,.033)>gait,"resume clears landing hold");
  }
  using(var a=AnimalAnimation.Render(0,0,220,0,0,1,false,0,0,0))using(var b=AnimalAnimation.Render(0,0,220,3.7,0,1,false,0,0,0)){
   bool feet=true;for(int y=195;y<220;y++)for(int x=0;x<220;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))feet=false;Check(feet,"cat idle keeps feet planted");
  }
  File.WriteAllText("animal-test-results.txt",checks+" animal render/phase/ground checks passed.\r\n");Console.WriteLine(checks+" animal checks passed");
 }
}
