using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
class BehaviorTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 [STAThread]static void Main(){
  var b=new CompanionBehavior(0);
  b.Update(59.99,0,true,false,false);Check(!b.IsWaving(59.99)&&!b.IsWalking(59.99),"no early action");
  b.Update(60,0,true,false,false);Check(b.IsWaving(60)&&b.WaveUntil==66,"wave at minute one");
  b.Update(66,0,true,false,false);Check(!b.IsWaving(66),"six-second wave");
  b.Update(120,0,true,false,false);Check(b.IsWaving(120),"second wave");
  b.Update(180,0,true,false,false);Check(b.IsWalking(180)&&!b.IsWaving(180),"walk priority at minute three");
  b.Update(197.99,0,true,false,false);Check(b.IsWalking(197.99),"18 second walking");
  b.Update(198,0,true,false,false);Check(!b.IsWalking(198)&&b.NextWalk==378&&b.NextWave==258,"stationary cycle restarts");
  b.Update(300,299.99,true,false,false);Check(!b.Sleeping,"no early sleep");
  b.Update(300,300,true,false,false);Check(b.Sleeping&&!b.IsWaving(300)&&!b.IsWalking(300),"sleep at five minutes");
  b.Update(301,0,true,false,false);Check(!b.Sleeping&&b.NextWave==361&&b.NextWalk==481,"input wakes and resets timers");
  b.Update(302,0,true,true,false);Check(b.Sleeping,"manual sleep");
  b.Update(303,0,true,true,false);Check(b.Sleeping,"manual sleep stays despite input");
  b.Update(304,0,true,false,false);Check(!b.Sleeping,"manual wake");
  b.Greet(400);Check(b.IsWaving(405.99)&&!b.IsWaving(406),"click wave six seconds");
  b.Reset(0);b.Update(180,0,false,false,false);Check(!b.IsWalking(180),"roaming disabled");
  b.Reset(0);b.Update(180,0,true,false,true);Check(!b.IsWalking(180)&&!b.IsWaving(180),"menu blocks behavior");
  b.Reset(0);b.Update(10000,10000,true,false,false);Check(b.Sleeping,"long suspension sleeps immediately");
  var transition=new PoseTransition();
  foreach(int target in new[]{2,0,1,0,3,0}){
   for(int i=0;i<50;i++){transition.Update(.01,target);Check(transition.Current>=0&&transition.Current<=3,"valid single pose");Check(transition.Compression>=0&&transition.Compression<=.171,"finite compression");}
   Check(transition.Current==target&&!transition.Active,"transition settles");
  }
  foreach(var screen in new[]{new Rectangle(0,0,1920,1080),new Rectangle(-2560,0,2560,1440),new Rectangle(0,-1920,1080,1920),new Rectangle(0,0,3840,2160)}){
   foreach(var point in new[]{new Point(screen.Left+screen.Width/2,screen.Top+screen.Height/2),new Point(screen.Right-100,screen.Bottom-100),screen.Location}){
    var zone=ActivityArea.Around(point,new Size(220,220),screen);
    Check(screen.Contains(zone),"zone on monitor");Check(Math.Abs((double)zone.Width*zone.Height/(screen.Width*screen.Height)-.125)<.001,"one eighth area");
    var location=PetForm.Clamp(new Point(-10000,10000),new Size(220,220),zone);Check(zone.Contains(new Rectangle(location,new Size(220,220))),"pet wholly inside zone");
   }
  }
  Sprites.Load();GirlAnimation.Load();
  // Fractional previous fade weights must select ONE intact pose, never blend silhouettes.
  foreach(var weights in new[]{new[]{.5,.5,.5},new[]{.5,.8,0.0},new[]{.7,.2,0.0},new[]{.1,.2,.1}}){
   int selected=weights[2]>=.5?3:weights[1]>=.5?2:weights[0]>=.25?1:0;
   using(var a=GirlAnimation.Render(0,220,1,2,weights[0],1,0,weights[1],false,false,0,weights[2]))
   using(var c=GirlAnimation.RenderPose(selected,220,1,2,1,0,false,false,0,0)){
    bool same=true;for(int y=0;y<220;y++)for(int x=0;x<220;x++)if(a.GetPixel(x,y)!=c.GetPixel(x,y))same=false;
    Check(same,"single silhouette at fractional transition");
   }
  }
  using(var f=new PetForm(new Preferences {Persist=false},false)){
   f.Show();Application.DoEvents();f.ResizePet(280);f.Switch(1);f.BringHome();Application.DoEvents();Check(f.Visible,"native startup resize switch home");f.Close();
  }
  using(var sheet=new Bitmap(1320,440))using(var g=Graphics.FromImage(sheet)){
   g.Clear(Color.FromArgb(45,47,57));var t=new PoseTransition();
   for(int i=0;i<12;i++){t.Update(.042,i<6?2:0);using(var frame=GirlAnimation.RenderPose(t.Current,220,i*.042,0,1,0,false,false,0,t.Compression))g.DrawImageUnscaled(frame,i%6*220,i/6*220);}sheet.Save("transition-preview.png");
  }
  File.WriteAllText("behavior-test-results.txt",checks+" behavior/transition/area checks passed.\r\nInput timer via GetLastInputInfo: "+UserActivity.IdleSeconds.ToString("F2")+" seconds.\r\n");Console.WriteLine(checks+" checks passed");
 }
}
