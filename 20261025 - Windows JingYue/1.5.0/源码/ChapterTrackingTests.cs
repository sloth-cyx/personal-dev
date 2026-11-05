using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace JingYue {
 public static class ChapterTrackingTests {
  const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
  static T Field<T>(MainWindow w,string name){return (T)typeof(MainWindow).GetField(name,Flags).GetValue(w);}
  static object Call(MainWindow w,string name,params object[] values){try{return typeof(MainWindow).GetMethod(name,Flags).Invoke(w,values);}catch(TargetInvocationException ex){throw ex.InnerException;}}
  static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
  static void Await(Task task){while(!task.IsCompleted)Pump();task.GetAwaiter().GetResult();Pump();}
  static void Check(bool ok,string label,StringBuilder log){if(!ok)throw new Exception("FAIL: "+label);log.AppendLine("PASS: "+label);}
  static T Visual<T>(DependencyObject parent)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T)return (T)child;var found=Visual<T>(child);if(found!=null)return found;}return null;}
  public static void Run(StringBuilder log){
   var list=new[]{new Paragraph{Index=10,Text="第一章"},new Paragraph{Index=110,Text="第二章"},new Paragraph{Index=210,Text="第三章"}};
   Check(ChapterLookup.At(list,0)==null&&ChapterLookup.At(list,10)==list[0]&&ChapterLookup.At(list,109)==list[0]&&ChapterLookup.At(list,110)==list[1]&&ChapterLookup.At(list,999)==list[2]&&ChapterLookup.At(new Paragraph[0],1)==null,"Chapter lookup handles boundaries, preface and unstructured text",log);
   string root=Path.Combine(Path.GetTempPath(),"JingYueChapterTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);MainWindow window=null;var previousContext=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
   try{
    var doc=new Document{EncodingName="UTF-8"};for(int i=0;i<850;i++){bool heading=i==10||i==110||i==210||i>=310&&(i-310)%20==0;string text=i==10?"第一章 起点":i==110?"第二章 远方":i==210?"第三章 星河":heading?"后续章节 "+i:"段落 "+i+"：阅读沿着这一行继续，目录高亮不应改变正文的位置。";var p=new Paragraph{Index=i,Text=text,Kind=heading?1:0};doc.Blocks.Add(p);doc.Characters+=text.Length;if(heading)doc.Chapters.Add(p);}
    string source=Path.Combine(root,"chapter-test.txt");File.WriteAllText(source,"测试书源");var book=new Book{Path=source,Title="章节联动测试",Position=125,VisualPart=0};book.Marks.Add(25);
    window=new MainWindow(root){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,ShowInTaskbar=false};var settings=Field<Settings>(window,"settings");settings.ReadingMode="pages";settings.Books.Add(book);window.Show();Pump();Await((Task)Call(window,"DisplayBook",book,doc));
    var nav=Field<ListBox>(window,"nav");var mode=Field<ComboBox>(window,"navMode");var headingText=Field<TextBlock>(window,"currentChapterTitle");
    Check(nav.SelectedItem==doc.Chapters[1]&&headingText.Text.Contains("第二章")&&Field<ReadingPage>(window,"page").Start.Unit==125,"Restored page updates chapter heading and TOC without rewinding to the chapter start",log);
    Call(window,"JumpLogical",260);Pump();Check(nav.SelectedItem==doc.Chapters[2]&&Field<ReadingPage>(window,"page").Start.Unit==260&&headingText.Text.Contains("第三章"),"Mid-chapter jumps synchronize selection without recursive navigation",log);
    mode.SelectedIndex=1;Call(window,"JumpLogical",50);Pump();Check(mode.SelectedIndex==1&&headingText.Text.Contains("第一章"),"Chapter updates preserve the user's bookmark tab",log);mode.SelectedIndex=0;Pump();Check(nav.SelectedItem==doc.Chapters[0]&&Field<ReadingPage>(window,"page").Start.Unit==50,"Returning to TOC selects the current chapter without moving the page",log);
    nav.SelectedItem=doc.Chapters[1];Pump();Check(Field<ReadingPage>(window,"page").Start.Unit==110&&headingText.Text.Contains("第二章"),"Clicking a chapter still navigates to its start",log);
    Call(window,"SetReadingMode",false);Pump();Call(window,"JumpLogical",165);Pump();Call(window,"CaptureReadingPosition");Check(nav.SelectedItem==doc.Chapters[1]&&headingText.Text.Contains("第二章")&&book.Position>=160&&book.Position<180,"Continuous scrolling tracks the chapter and retains the paragraph position",log);
    var reader=Field<ListBox>(window,"reader");var scroll=Visual<ScrollViewer>(reader);scroll.ScrollToBottom();Pump();var last=doc.Chapters.Last();var container=nav.ItemContainerGenerator.ContainerFromItem(last)as ListBoxItem;bool visible=false;if(container!=null){double y=container.TransformToAncestor(nav).Transform(new Point()).Y;visible=y<nav.ActualHeight&&y+container.ActualHeight>0;}Check(nav.SelectedItem==last&&headingText.Text.Contains(last.Text)&&visible,"Scrolling to the end highlights and reveals the current chapter in a long TOC",log);
    scroll.ScrollToTop();Pump();Check(nav.SelectedItem==null&&headingText.Text.Contains("卷首"),"Preface before the first chapter clears the previous TOC selection",log);
    book.Position=165;book.VisualPart=0;book.PageCharacter=0;book.ParagraphOffset=0;settings.ReadingMode="pages";Await((Task)Call(window,"DisplayBook",book,doc));Check(nav.SelectedItem==doc.Chapters[1]&&headingText.Text.Contains("第二章"),"Reopening a saved reading location restores the current chapter immediately",log);
    doc.Chapters.Clear();Call(window,"RefreshNav");Call(window,"UpdateReadingProgress");Check(nav.SelectedItem==null&&headingText.Text.Contains("未检测到章节"),"Books without headings show a clear fallback instead of a stale chapter",log);
   }finally{if(window!=null){Field<DispatcherTimer>(window,"saveTimer").Stop();window.Close();Pump();}SynchronizationContext.SetSynchronizationContext(previousContext);if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
  }
 }
}
