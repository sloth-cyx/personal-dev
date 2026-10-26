using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace JingYue {
 public class ReadingUnit {
  public Paragraph Paragraph;public ReadingImage Image;public int Index,LogicalIndex,Part,TextStart;public double MaxImageHeight=650;
  public static List<ReadingUnit> Build(Document document){var result=new List<ReadingUnit>();foreach(var p in document.Blocks){int part=0;foreach(var image in p.BeforeImages)result.Add(new ReadingUnit{Image=image,LogicalIndex=p.Index,Part=part++});if(!String.IsNullOrEmpty(p.Text))result.Add(new ReadingUnit{Paragraph=p,LogicalIndex=p.Index,Part=part++});foreach(var image in p.AfterImages)result.Add(new ReadingUnit{Image=image,LogicalIndex=p.Index,Part=part++});}for(int i=0;i<result.Count;i++)result[i].Index=i;return result;}
  public static int Find(List<ReadingUnit> units,int logical,int part){int start=units.FindIndex(u=>u.LogicalIndex>=logical);if(start<0)return Math.Max(0,units.Count-1);int end=start;while(end+1<units.Count&&units[end+1].LogicalIndex==units[start].LogicalIndex)end++;return Math.Min(end,start+Math.Max(0,part));}
 }
 public struct ReadingCursor:IEquatable<ReadingCursor> {public int Unit,Character;public ReadingCursor(int unit,int character){Unit=unit;Character=character;}public bool Equals(ReadingCursor other){return Unit==other.Unit&&Character==other.Character;}}
 public class ReadingPage {public ReadingCursor Start,Next;public List<ReadingUnit> Pieces=new List<ReadingUnit>();public bool End;}
 public static class ImageLayout {
  public static double Height(ReadingImage image,double width,double maxHeight){double w=image.PixelWidth>0?image.PixelWidth:640,h=image.PixelHeight>0?image.PixelHeight:400;double available=Math.Max(80,width-40);return Math.Max(1,Math.Min(maxHeight,Math.Min(w,available)*h/w));}
  public static double Total(ReadingImage image,double width,double maxHeight){return Height(image,width,maxHeight)+92;}
 }
 public class PageEngine {
  readonly List<ReadingUnit> units;readonly double width,height,size,line;readonly string font;
  public PageEngine(List<ReadingUnit> units,double width,double height,double size,double line,string font){this.units=units;this.width=width;this.height=Math.Max(120,height);this.size=size;this.line=line;this.font=font;}
  public static int SafeBoundary(string text,int index){index=Math.Max(0,Math.Min(index,text.Length));if(index>0&&index<text.Length&&Char.IsHighSurrogate(text[index-1])&&Char.IsLowSurrogate(text[index]))index--;return index;}
  double Measure(Paragraph p,string text){var part=new Paragraph{Text=text,Kind=p.Kind,Markdown=p.Markdown};var block=new ReaderText{DataContext=part};block.FontSize=size*(p.Kind==1?1.5:p.Kind==2?1.18:1);block.FontFamily=new FontFamily(p.Kind==4?"Consolas":font);block.LineHeight=size*line;block.Measure(new Size(width,Double.PositiveInfinity));return block.DesiredSize.Height;}
  ReadingUnit Piece(ReadingUnit source,int start,int count){return new ReadingUnit{Index=source.Index,LogicalIndex=source.LogicalIndex,Part=source.Part,TextStart=start,Paragraph=new Paragraph{Text=source.Paragraph.Text.Substring(start,count),Kind=source.Paragraph.Kind,Markdown=source.Paragraph.Markdown,Index=source.LogicalIndex}};}
  public ReadingPage Build(ReadingCursor cursor){var page=new ReadingPage{Start=cursor};int i=Math.Max(0,cursor.Unit),offset=Math.Max(0,cursor.Character);double used=0;while(i<units.Count){var unit=units[i];double remaining=height-used;if(unit.Image!=null){double max=Math.Max(40,height-92);double needed=ImageLayout.Total(unit.Image,width,max);if(page.Pieces.Count>0&&needed>remaining+.1)break;page.Pieces.Add(new ReadingUnit{Index=unit.Index,LogicalIndex=unit.LogicalIndex,Part=unit.Part,Image=unit.Image,MaxImageHeight=max});used+=needed;i++;offset=0;continue;}
    string text=unit.Paragraph.Text;offset=SafeBoundary(text,offset);if(offset>=text.Length){i++;offset=0;continue;}double full=Measure(unit.Paragraph,text.Substring(offset));if(offset==0&&(unit.Paragraph.Kind==1||unit.Paragraph.Kind==2)&&page.Pieces.Count>0&&i+1<units.Count&&units[i+1].Paragraph!=null&&full+Measure(units[i+1].Paragraph,units[i+1].Paragraph.Text.Substring(0,Math.Min(1,units[i+1].Paragraph.Text.Length)))>remaining+.1)break;if(full<=remaining+.1){page.Pieces.Add(Piece(unit,offset,text.Length-offset));used+=full;i++;offset=0;continue;}
    int low=0,high=text.Length-offset;while(low<high){int mid=(low+high+1)/2;int safe=SafeBoundary(text,offset+mid)-offset;if(safe<=0){high=mid-1;continue;}if(Measure(unit.Paragraph,text.Substring(offset,safe))<=remaining+.1)low=mid;else high=mid-1;}
    int take=SafeBoundary(text,offset+low)-offset;if(take==0&&page.Pieces.Count>0)break;if(take==0)take=Char.IsHighSurrogate(text[offset])&&offset+1<text.Length?2:1;page.Pieces.Add(Piece(unit,offset,take));offset+=take;break;
   }page.Next=new ReadingCursor(i,offset);page.End=i>=units.Count;return page;
  }
  public ReadingCursor Previous(ReadingCursor current){int i=Math.Min(current.Unit,units.Count-1),end=current.Character;double used=0;if(current.Unit>=units.Count){i=units.Count-1;end=units[i].Paragraph==null?0:units[i].Paragraph.Text.Length;}else if(end==0){i--;if(i>=0)end=units[i].Paragraph==null?0:units[i].Paragraph.Text.Length;}
   ReadingCursor result=current;while(i>=0){var u=units[i];double room=height-used;if(u.Image!=null){double needed=ImageLayout.Total(u.Image,width,Math.Max(40,height-92));if(needed>room+.1&&!result.Equals(current))break;result=new ReadingCursor(i,0);used+=needed;}
    else{string text=u.Paragraph.Text;end=Math.Max(0,Math.Min(end,text.Length));double full=Measure(u.Paragraph,text.Substring(0,end));if(full<=room+.1){used+=full;result=new ReadingCursor(i,0);}else{int low=0,high=end;while(low<high){int mid=(low+high+1)/2;int start=SafeBoundary(text,end-mid);if(Measure(u.Paragraph,text.Substring(start,end-start))<=room+.1)low=mid;else high=mid-1;}int begin=SafeBoundary(text,end-low);if(begin<end)result=new ReadingCursor(i,begin);break;}}
    i--;if(i>=0)end=units[i].Paragraph==null?0:units[i].Paragraph.Text.Length;
   }return result;
  }
 }
}
