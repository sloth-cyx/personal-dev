using System;
using System.IO;
class TurnTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static void Main(){
  // Reproduce v0.3.1: resetting fractions while on an edge prevents departure.
  double speed=0,fraction=0;int oldLeft=100;
  for(int i=0;i<90;i++){speed+=(-43-speed)*(1-Math.Exp(-.033*4.2));fraction+=speed*.033;int shift=(int)fraction;fraction-=shift;oldLeft+=shift;if(oldLeft>=100){oldLeft=100;speed=0;fraction=0;}}
  Check(oldLeft==100,"old failure reproduced");
  foreach(double dt in new[]{.008,.016,.033,.04,.06})foreach(int start in new[]{-500,-450,-400})foreach(int initialDirection in new[]{-1,1}){
   var m=new GirlMotion{Speed=initialDirection*43};int direction=initialDirection,left=start,turns=0;double travelled=0;
   for(int i=0;i<1800;i++){
    int beforeDirection=direction;double before=left+m.PositionX;
    m.Step(dt,direction,true,false,false,false);left=m.AdvanceHorizontal(left,-500,-400,dt,ref direction);
    double after=left+m.PositionX;Check(after>=-500&&after<=-400,"inside bounds");Check(Math.Abs(m.Speed)>42.99,"speed preserved across turns");
    if(beforeDirection!=direction)turns++;
    else {Check(Math.Abs(Math.Abs(after-before)-43*dt)<.00001,"equal forward and return pace");travelled+=Math.Abs(after-before);}
   }
   Check(turns>=5&&travelled>500,"repeated departures from both edges");
  }
  var one=new GirlMotion{Speed=-43};int d=-1;int x=one.AdvanceHorizontal(100,0,100,.008,ref d);Check(x<100&&one.PositionX>0&&one.Speed<0,"subpixel edge departure");
  one.PositionX=0;one.Speed=43;d=1;x=one.AdvanceHorizontal(0,0,1,.06,ref d);Check(x>=0&&x<=1,"overshoot in narrow range");
  x=one.AdvanceHorizontal(0,0,0,.033,ref d);Check(x==0&&one.Speed==0,"no available travel width");
  File.WriteAllText("turn-test-results.txt",checks+" continuous-position turn checks passed across 8/16/33/40/60 ms frames, negative coordinates, both directions and repeated boundaries.\r\nOld edge stall reproduced before correction.\r\n");Console.WriteLine(checks+" turn checks passed");
 }
}
