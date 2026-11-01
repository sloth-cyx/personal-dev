using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
class MotionTests {
 [STAThread]static void Main(){Sprites.Load();GirlAnimation.Load();Directory.CreateDirectory("frames");int checks=0;
 var m=new GirlMotion();m.Step(1.0/30,1,true,false,false,false);Check(m.Speed>0&&m.Speed<43,"acceleration");checks++;
 for(int i=0;i<120;i++)m.Step(1.0/30,1,true,false,false,false);Check(m.Speed>42&&m.Gait>1,"distance gait");checks++;
 for(int i=0;i<90;i++)m.Step(1.0/30,1,false,false,false,false);Check(m.Speed==0&&m.Walk<.01,"stop settling");checks++;
 m.Step(.033,-1,true,true,false,false);Check(m.Speed==0,"drag suspension");checks++;
 for(int i=0;i<90;i++)m.Step(.033,-1,true,false,false,false);Check(m.Speed< -42,"reverse");checks++;
 Check(GirlAnimation.Blink(5.04)>.99&&GirlAnimation.Blink(4)<.01,"blink timing");checks++;
 var watch=Stopwatch.StartNew();
 using(var sheet=new Bitmap(1320,660))using(var g=Graphics.FromImage(sheet)){
  g.Clear(Color.FromArgb(44,48,60));
  for(int i=0;i<12;i++)using(var b=GirlAnimation.Render(0,220, i*.13,i*Math.PI/3,1,1,i==8?1:0,i>=9?1:0,false,false,0))g.DrawImageUnscaled(b,(i%6)*220,(i/6)*330);
  sheet.Save("motion-preview.png");
 }
 for(int i=0;i<60;i++){
  double t=i/30.0;using(var b=GirlAnimation.Render(0,280,t,t*Math.PI*2,1,1,0,0,false,false,0)){Check(b.GetPixel(0,0).A==0,"alpha");b.Save("frames/walk-"+i.ToString("D3")+".png");checks++;}
 }
 long renderTime=watch.ElapsedMilliseconds;
 using(var a=GirlAnimation.Render(0,220,0,Math.PI/2,1,1,0,0,false,false,0))
 using(var b=GirlAnimation.Render(0,220,0,Math.PI*1.5,1,1,0,0,false,false,0)){
  int diffs=0;for(int y=165;y<210;y++)for(int x=0;x<220;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))diffs++;
  Check(diffs>50,"alternating legs");checks++;
 }
 foreach(int p in new[]{0,2})foreach(int size in new[]{110,130,180,220,280,320})using(var b=GirlAnimation.Render(p,size,5.04,0,0,0,1,0,false,false,1)){Check(b.Width==size,"sizes");checks++;}
 var result=checks+" motion checks passed. 72 preview renders + PNG encoding: "+renderTime+" ms.\r\n";
 File.WriteAllText("motion-test-results.txt",result);Console.WriteLine(result);
 }
 static void Check(bool valid,string name){if(!valid)throw new Exception(name);}
}
