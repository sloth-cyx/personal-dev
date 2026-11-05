using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly: AssemblyTitle("ScreenPen")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace ScreenPen {
static class Native {
 [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h,int n);
 [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr h,int n,int v);
 [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint m,uint k);
 [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int n);
 [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
 [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window,IntPtr dc);
 [DllImport("gdi32.dll",SetLastError=true)] internal static extern bool BitBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sourceX,int sourceY,uint operation);
}
static class Assets {
 internal static ImageSource Icon;
 static Assets() {
  using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.png")) {
   BitmapImage b=new BitmapImage();b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;b.StreamSource=s;b.EndInit();b.Freeze();Icon=b;
  }
 }
 internal static Brush Brush(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));}
}
enum Tool {Pen,Highlighter,Eraser,Text,Note}
class Mark {
 internal Stroke Ink;internal bool Highlight,Note;internal string Text="";internal Point Position;internal Color Color;internal double FontSize=24;
 internal Mark Clone(){return new Mark {Ink=Ink==null?null:Ink.Clone(),Highlight=Highlight,Note=Note,Text=Text,Position=Position,Color=Color,FontSize=FontSize};}
 internal FormattedText Layout(){
  FormattedText f=new FormattedText(Text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),FontSize,Note?Assets.Brush("#252C39"):new SolidColorBrush(Color));
  f.MaxTextWidth=Note?260:700;f.MaxTextHeight=1000;f.Trimming=TextTrimming.CharacterEllipsis;return f;
 }
 internal Rect Bounds {get {FormattedText f=Layout();return new Rect(Position,new Size(Note?284:Math.Max(12,f.WidthIncludingTrailingWhitespace),Math.Max(FontSize+4,f.Height)+(Note?24:0)));}}
 internal Rect InteractionBounds {get {Rect b=Bounds;b.Inflate(7,7);return b;}}
 internal bool IsMoveHandle(Point point){Rect interior=Bounds;if(Note)interior.Inflate(-10,-10);return InteractionBounds.Contains(point)&&!interior.Contains(point);}
}
class Paper : FrameworkElement {
 internal List<Mark> Marks=new List<Mark>();
 readonly List<List<Mark>> undo=new List<List<Mark>>();
 internal Tool Tool=Tool.Pen;internal bool Drawing=true,Hidden;
 internal Color Color=(Color)ColorConverter.ConvertFromString("#FF4545");internal double Size=4,TextSize=24;
 internal Action<Point,Mark> EditRequested;internal Func<Point,bool> OverToolbar;internal event Action Changed;
 Mark current,moving,hovered;Point previous,start,original;bool erasing,moveRecorded;
 internal bool CanUndo {get{return undo.Count>0;}}
 internal Paper(){Focusable=true;Cursor=Cursors.Pen;}
 static List<Mark> Copy(List<Mark> src){return src.ConvertAll(m=>m.Clone());}
 internal void Remember(){undo.Add(Copy(Marks));if(undo.Count>50)undo.RemoveAt(0);}
 internal void Notify(){InvalidateVisual();if(Changed!=null)Changed();}
 internal void Finish(){current=null;moving=null;erasing=false;moveRecorded=false;if(IsMouseCaptured)ReleaseMouseCapture();}
 internal void ClearHover(){hovered=null;InvalidateVisual();}
 internal void Undo(){Finish();ClearHover();if(undo.Count>0){Marks=undo[undo.Count-1];undo.RemoveAt(undo.Count-1);}Notify();}
 internal void Clear(){Finish();ClearHover();if(Marks.Count>0){Remember();Marks.Clear();}Notify();}
 internal void Select(Tool tool){Finish();ClearHover();Tool=tool;Cursor=DefaultCursor();Notify();}
 Cursor DefaultCursor(){return !Drawing||Hidden?Cursors.Arrow:Tool==Tool.Text||Tool==Tool.Note?Cursors.IBeam:Tool==Tool.Eraser?Cursors.Cross:Cursors.Pen;}
 internal Mark HitText(Point p){for(int i=Marks.Count-1;i>=0;i--)if(Marks[i].Ink==null&&Marks[i].InteractionBounds.Contains(p))return Marks[i];return null;}
 internal Cursor CursorAt(Point p){
  if(!Drawing||Hidden||(OverToolbar!=null&&OverToolbar(p)))return Cursors.Arrow;
  if(Tool==Tool.Eraser)return Cursors.Cross;
  Mark hit=HitText(p);return hit==null?DefaultCursor():hit.IsMoveHandle(p)?Cursors.SizeAll:Cursors.IBeam;
 }
 void Hover(Point p){Mark next=Drawing&&!Hidden&&Tool!=Tool.Eraser?HitText(p):null;if(hovered!=next){hovered=next;InvalidateVisual();}Cursor=CursorAt(p);}
 bool ToolbarAt(Point p){return OverToolbar!=null&&OverToolbar(p);}
 protected override void OnMouseDown(MouseButtonEventArgs e){
  Point p=e.GetPosition(this);
  if(!Drawing||Hidden||ToolbarAt(p))return;
  Focus();
  if(e.ChangedButton==MouseButton.Right){Undo();e.Handled=true;return;}if(e.ChangedButton!=MouseButton.Left)return;
  Mark hit=Tool==Tool.Eraser?null:HitText(p);
  if(hit!=null){
   if(hit.IsMoveHandle(p)){moving=hit;start=p;original=hit.Position;moveRecorded=false;Cursor=Cursors.SizeAll;CaptureMouse();}
   else if(EditRequested!=null){Finish();ClearHover();EditRequested(p,hit);}
   e.Handled=true;return;
  }
  if(Tool==Tool.Text||Tool==Tool.Note){if(EditRequested!=null)EditRequested(p,null);e.Handled=true;return;}
  Remember();previous=p;
  if(Tool==Tool.Eraser){erasing=true;Erase(p,p);}
  else {
   current=new Mark {Color=Color,Highlight=Tool==Tool.Highlighter};
   current.Ink=new Stroke(new StylusPointCollection(new[]{new StylusPoint(p.X,p.Y)}));
   current.Ink.DrawingAttributes=new DrawingAttributes {Color=Color,Width=Tool==Tool.Highlighter?Size*5:Size,Height=Tool==Tool.Highlighter?Size*5:Size,FitToCurve=true,StylusTip=StylusTip.Ellipse,IgnorePressure=true};Marks.Add(current);
  }
  CaptureMouse();Notify();e.Handled=true;
 }
 protected override void OnMouseMove(MouseEventArgs e){
  Point p=e.GetPosition(this);
  if(!Drawing||Hidden){Cursor=Cursors.Arrow;return;}
  if(ToolbarAt(p)){Finish();ClearHover();Cursor=Cursors.Arrow;return;}
  if(e.LeftButton!=MouseButtonState.Pressed){if(IsMouseCaptured)Finish();Hover(p);return;}
  if(moving!=null){
   Vector delta=p-start;
   if(!moveRecorded){if(delta.Length<3)return;Remember();moveRecorded=true;}
   Rect bounds=moving.Bounds;
   moving.Position=new Point(Math.Max(0,Math.Min(Math.Max(0,ActualWidth-bounds.Width),original.X+delta.X)),Math.Max(0,Math.Min(Math.Max(0,ActualHeight-bounds.Height),original.Y+delta.Y)));
   Cursor=Cursors.SizeAll;InvalidateVisual();return;
  }
  if(current!=null){if(HitText(p)!=null){Finish();Hover(p);}else{current.Ink.StylusPoints.Add(new StylusPoint(p.X,p.Y));InvalidateVisual();}}
  else if(erasing){Erase(previous,p);previous=p;}
  else Hover(p);
 }
 protected override void OnMouseUp(MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left){Finish();Hover(e.GetPosition(this));Notify();}}
 protected override void OnLostMouseCapture(MouseEventArgs e){current=null;moving=null;erasing=false;moveRecorded=false;base.OnLostMouseCapture(e);}
 protected override void OnMouseLeave(MouseEventArgs e){ClearHover();Cursor=DefaultCursor();base.OnMouseLeave(e);}
 internal void Erase(Point from,Point to){
  double diameter=Math.Max(20,Size*8);EllipseStylusShape shape=new EllipseStylusShape(diameter,diameter);Point[] path={from,to};List<Mark> result=new List<Mark>();
  foreach(Mark mark in Marks){
   if(mark.Ink!=null){if(!mark.Ink.HitTest(path,shape))result.Add(mark);else foreach(Stroke part in mark.Ink.GetEraseResult(path,shape))result.Add(new Mark {Ink=part,Highlight=mark.Highlight,Color=mark.Color});}
   else{Rect b=mark.Bounds;b.Inflate(diameter/2,diameter/2);Rect segment=new Rect(from,to);if(!b.Contains(from)&&!b.Contains(to)&&!b.IntersectsWith(segment))result.Add(mark);}
  }
  Marks=result;ClearHover();Notify();
 }
 internal void CommitText(Point p,Mark existing,string text,bool note,double size){
  if(String.IsNullOrWhiteSpace(text))return;Remember();if(existing==null){existing=new Mark {Position=p,Note=note,Color=Color};Marks.Add(existing);}existing.Text=text;existing.FontSize=size;Notify();
 }
 protected override void OnRender(DrawingContext dc){
  dc.DrawRectangle(Drawing&&!Hidden?new SolidColorBrush(Color.FromArgb(1,0,0,0)):Brushes.Transparent,null,new Rect(RenderSize));if(Hidden)return;
  foreach(Mark mark in Marks){
   if(mark.Ink!=null){dc.PushOpacity(mark.Highlight?.35:1);mark.Ink.Draw(dc);dc.Pop();}
   else{if(mark.Note){Rect shadow=mark.Bounds;shadow.Offset(3,4);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(65,0,0,0)),null,shadow,8,8);dc.DrawRoundedRectangle(Assets.Brush("#FFF0A6"),new Pen(Assets.Brush("#E6C85B"),1),mark.Bounds,7,7);}dc.DrawText(mark.Layout(),mark.Position+(mark.Note?new Vector(12,12):new Vector()));}
  }
  if(hovered!=null){Pen frame=new Pen(Assets.Brush("#38BDF8"),1){DashStyle=DashStyles.Dash};dc.DrawRoundedRectangle(null,frame,hovered.InteractionBounds,4,4);}
 }
}
class Overlay : Window {
 internal readonly Paper Paper=new Paper();internal IntPtr Handle;
 internal Overlay(bool test){
  Title="ScreenPen Canvas";Icon=Assets.Icon;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=test;Content=Paper;SetBounds();if(test){Left=450;Top=300;Width=1000;Height=650;}
  Loaded+=delegate{Handle=new WindowInteropHelper(this).EnsureHandle();if(!test)Native.SetWindowLong(Handle,-20,Native.GetWindowLong(Handle,-20)|0x80);};
 }
 internal void SetBounds(){Left=SystemParameters.VirtualScreenLeft;Top=SystemParameters.VirtualScreenTop;Width=SystemParameters.VirtualScreenWidth;Height=SystemParameters.VirtualScreenHeight;}
 internal void SetDrawing(bool value){Paper.Finish();Paper.ClearHover();Paper.Drawing=value;int style=Native.GetWindowLong(Handle,-20);Native.SetWindowLong(Handle,-20,value&&!Paper.Hidden?style&~0x20:style|0x20);Paper.Notify();}
}
class TextEditor : Window {
 internal TextBox Input;internal double TextSize;ComboBox sizes;
 internal TextEditor(Window owner,Mark mark,bool note,double size){
  Title=note?"ScreenPen 便签":"ScreenPen 文字";Icon=Assets.Icon;Owner=owner;Topmost=true;Width=460;Height=330;ResizeMode=ResizeMode.CanResizeWithGrip;WindowStartupLocation=WindowStartupLocation.CenterScreen;MinWidth=360;MinHeight=280;Background=Assets.Brush("#181D29");Foreground=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
  DockPanel root=new DockPanel {Margin=new Thickness(18)};Content=root;TextBlock hint=new TextBlock {Text="输入内容；Ctrl+Enter 确定，Esc 取消",Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
  StackPanel bottom=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
  bottom.Children.Add(new TextBlock {Text="字号 ",VerticalAlignment=VerticalAlignment.Center});sizes=new ComboBox {Width=65,Margin=new Thickness(0,0,12,0)};foreach(int n in new[]{16,20,24,32,40,48})sizes.Items.Add(n);sizes.SelectedItem=(int)size;if(sizes.SelectedIndex<0)sizes.SelectedItem=24;bottom.Children.Add(sizes);
  Button ok=Toolbar.MakeButton("确定",delegate{if(!String.IsNullOrWhiteSpace(Input.Text)){TextSize=(int)sizes.SelectedItem;DialogResult=true;}});Button cancel=Toolbar.MakeButton("取消",delegate{DialogResult=false;});cancel.IsCancel=true;bottom.Children.Add(ok);bottom.Children.Add(cancel);
  Input=new TextBox {Text=mark==null?"":mark.Text,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontSize=20,Padding=new Thickness(10),MaxLength=4000};System.Windows.Automation.AutomationProperties.SetName(Input,"内容");root.Children.Add(Input);
  Loaded+=delegate{Input.Focus();Input.SelectAll();};PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){if(!String.IsNullOrWhiteSpace(Input.Text)){TextSize=(int)sizes.SelectedItem;DialogResult=true;}e.Handled=true;}};
 }
}
class Toolbar : Window {
 readonly Overlay overlay;Paper paper {get{return overlay.Paper;}}
 readonly Dictionary<Tool,Button> toolButtons=new Dictionary<Tool,Button>();readonly List<int> registered=new List<int>();
 Button mode,hide,expand,undoButton;TextBlock status,caption;Border extra;IntPtr handle;HwndSourceHook hook;bool initialized,quitting,busy,previousDrawing,expanded;string warnings="",message="";
 internal Toolbar(Overlay canvas){
  overlay=canvas;Title="ScreenPen 1.2 屏幕画笔";Icon=Assets.Icon;Cursor=Cursors.Arrow;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;Width=388;SizeToContent=SizeToContent.Height;Topmost=true;Background=Assets.Brush("#181D29");Foreground=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;Left=SystemParameters.WorkArea.Left+24;Top=SystemParameters.WorkArea.Top+24;
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"--test-overlap")>=0){Left=480;Top=330;}
  StackPanel root=new StackPanel {Margin=new Thickness(10)};Content=root;DockPanel header=new DockPanel {Margin=new Thickness(0,0,0,6)};root.Children.Add(header);
  Button exit=MakeButton("×",delegate{Close();});exit.ToolTip="退出 ScreenPen";DockPanel.SetDock(exit,Dock.Right);header.Children.Add(exit);
  expand=MakeButton("展开 ▾",delegate{expanded=!expanded;extra.Visibility=expanded?Visibility.Visible:Visibility.Collapsed;expand.Content=expanded?"收起 ▴":"展开 ▾";});expand.ToolTip="展开或收起其他工具；快捷键始终可用";DockPanel.SetDock(expand,Dock.Right);header.Children.Add(expand);
  StackPanel brand=new StackPanel {Orientation=Orientation.Horizontal,Cursor=Cursors.SizeAll};brand.Children.Add(new Image {Source=Assets.Icon,Width=20,Height=20,Margin=new Thickness(3,0,7,0)});caption=new TextBlock {Text="ScreenPen 1.2",FontWeight=FontWeights.Bold,FontSize=14,VerticalAlignment=VerticalAlignment.Center};brand.Children.Add(caption);brand.MouseLeftButtonDown+=delegate{if(!busy)try{DragMove();}catch(InvalidOperationException){}};header.Children.Add(brand);
  Panel primary=new System.Windows.Controls.Primitives.UniformGrid {Rows=1,Columns=5};root.Children.Add(primary);AddTool(primary,Tool.Pen,"画笔");AddTool(primary,Tool.Highlighter,"荧光笔");AddTool(primary,Tool.Eraser,"橡皮擦");undoButton=MakeButton("撤销",paper.Undo);primary.Children.Add(undoButton);Button clear=MakeButton("清屏",paper.Clear);clear.ToolTip="清屏 · F9";primary.Children.Add(clear);
  StackPanel details=new StackPanel {Margin=new Thickness(0,8,0,0)};extra=new Border {Child=details,BorderBrush=Assets.Brush("#343E52"),BorderThickness=new Thickness(0,1,0,0),Margin=new Thickness(0,7,0,0),Visibility=Visibility.Collapsed};root.Children.Add(extra);
  StackPanel first=Row(details);mode=MakeButton("画笔模式 · F8",ToggleDrawing);first.Children.Add(mode);AddTool(first,Tool.Text,"文字");AddTool(first,Tool.Note,"便签");
  StackPanel second=Row(details);hide=MakeButton("隐藏批注",ToggleHidden);second.Children.Add(hide);second.Children.Add(MakeButton("全屏截图",Screenshot));Button hidePanel=MakeButton("隐藏工具栏",delegate{Hide();});hidePanel.ToolTip="Ctrl+Alt+H 恢复";second.Children.Add(hidePanel);
  StackPanel palette=Row(details);string[] colors={"#FF4545","#FFD83D","#39D98A","#38BDF8","#C084FC","#FFFFFF","#182030"};string[] names={"红色","黄色","绿色","蓝色","紫色","白色","深色"};
  for(int i=0;i<colors.Length;i++){Color c=(Color)ColorConverter.ConvertFromString(colors[i]);Button b=MakeButton("●",delegate{paper.Color=c;Update();});b.Width=31;b.Padding=new Thickness(2);b.Foreground=new SolidColorBrush(c);b.FontSize=23;b.ToolTip=names[i];System.Windows.Automation.AutomationProperties.SetName(b,names[i]);palette.Children.Add(b);}
  palette.Children.Add(new TextBlock {Text=" 粗细",VerticalAlignment=VerticalAlignment.Center});ComboBox width=new ComboBox {Width=52,Margin=new Thickness(4,6,0,6)};foreach(int n in new[]{2,4,7,11})width.Items.Add(n);width.SelectedItem=4;width.SelectionChanged+=delegate{paper.Size=(int)width.SelectedItem;};palette.Children.Add(width);
  status=new TextBlock {Margin=new Thickness(4,9,4,2),MaxWidth=350,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray};details.Children.Add(status);details.Children.Add(new TextBlock {Text="F8 鼠标/批注   F9 清屏   F10 / 右键撤销\nCtrl+Alt+B 隐藏批注   Ctrl+Alt+S 全屏截图\nCtrl+Alt+H 隐藏/显示工具栏",FontSize=11,Foreground=Brushes.LightSlateGray,Margin=new Thickness(4,6,4,0)});
  paper.Changed+=Update;paper.EditRequested=EditText;
  paper.OverToolbar=delegate(Point p){if(!IsVisible||!IsLoaded)return false;Point local=PointFromScreen(paper.PointToScreen(p));return new Rect(0,0,ActualWidth,ActualHeight).Contains(local);};
  MouseEnter+=delegate{Cursor=Cursors.Arrow;paper.Finish();paper.ClearHover();};
  Loaded+=delegate{if(initialized)return;initialized=true;handle=new WindowInteropHelper(this).EnsureHandle();hook=new HwndSourceHook(Messages);HwndSource.FromHwnd(handle).AddHook(hook);Register(1,0x4000,0x77,"F8");Register(2,0x4000,0x78,"F9");Register(3,0x4000,0x79,"F10");Register(4,0x4003,0x48,"Ctrl+Alt+H");Register(5,0x4003,0x42,"Ctrl+Alt+B");Register(6,0x4003,0x53,"Ctrl+Alt+S");};
  PreviewKeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Escape&&!busy){overlay.SetDrawing(false);Update();}};
  Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy){e.Cancel=true;return;}quitting=true;foreach(int id in registered)Native.UnregisterHotKey(handle,id);SystemEvents.DisplaySettingsChanged-=DisplayChanged;paper.ClearHover();overlay.Close();Application.Current.Shutdown();};
  SystemEvents.DisplaySettingsChanged+=DisplayChanged;Update();
 }
 void AddTool(Panel parent,Tool tool,string name){Button b=MakeButton(name,delegate{if(busy)return;message="";paper.Hidden=false;paper.Select(tool);overlay.SetDrawing(true);Update();});toolButtons.Add(tool,b);parent.Children.Add(b);}
 void ToggleDrawing(){if(busy)return;message="";if(paper.Hidden){paper.Hidden=false;overlay.SetDrawing(true);}else overlay.SetDrawing(!paper.Drawing);Update();}
 void ToggleHidden(){if(busy)return;message="";if(!paper.Hidden){previousDrawing=paper.Drawing;paper.Hidden=true;overlay.SetDrawing(false);}else{paper.Hidden=false;overlay.SetDrawing(previousDrawing);}Update();}
 internal void EditText(Point at,Mark existing){
  if(busy)return;bool wasDrawing=paper.Drawing;bool note=existing!=null?existing.Note:paper.Tool==Tool.Note;busy=true;overlay.SetDrawing(false);
  try{TextEditor editor=new TextEditor(this,existing,note,existing==null?(note?20:paper.TextSize):existing.FontSize);if(editor.ShowDialog()==true){paper.TextSize=editor.TextSize;paper.CommitText(at,existing,editor.Input.Text,note,editor.TextSize);}}
  finally{busy=false;overlay.SetDrawing(wasDrawing);Update();}
 }
 void Register(int id,uint mods,uint key,string name){if(Native.RegisterHotKey(handle,id,mods,key))registered.Add(id);else{warnings+="\n"+name+" 被占用，请展开后使用按钮。";Update();}}
 IntPtr Messages(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x312){if(!busy)switch(w.ToInt32()){case 1:ToggleDrawing();break;case 2:paper.Clear();break;case 3:paper.Undo();break;case 4:if(IsVisible)Hide();else{Show();Activate();}break;case 5:ToggleHidden();break;case 6:Screenshot();break;}handled=true;}return IntPtr.Zero;}
 void DisplayChanged(object s,EventArgs e){Dispatcher.BeginInvoke(new Action(delegate{if(quitting)return;paper.Clear();overlay.SetBounds();Left=SystemParameters.WorkArea.Left+24;Top=SystemParameters.WorkArea.Top+24;}));}
 void Screenshot(){
  if(busy)return;busy=true;bool visible=IsVisible;bool drawing=paper.Drawing;
  try{
   SaveFileDialog dialog=new SaveFileDialog {Title="保存全屏截图（包含当前可见批注）",Filter="PNG 图像 (*.png)|*.png",DefaultExt=".png",AddExtension=true,OverwritePrompt=true,FileName="ScreenPen-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)};
   overlay.SetDrawing(false);if(dialog.ShowDialog(this)!=true){busy=false;overlay.SetDrawing(drawing);return;}Hide();paper.ClearHover();
   DispatcherTimer capture=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(300)};capture.Tick+=delegate{capture.Stop();try{CaptureDesktop(dialog.FileName);message="截图已保存："+dialog.FileName;}catch(Exception ex){message="截图失败："+ex.Message;}finally{busy=false;overlay.SetDrawing(drawing);if(visible)Show();Update();}};capture.Start();
  }catch(Exception ex){busy=false;overlay.SetDrawing(drawing);if(visible)Show();message="截图失败："+ex.Message;Update();}
 }
 internal static void CaptureDesktop(string path){
  int x=Native.GetSystemMetrics(76),y=Native.GetSystemMetrics(77),width=Native.GetSystemMetrics(78),height=Native.GetSystemMetrics(79);
  using(System.Drawing.Bitmap bitmap=new System.Drawing.Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb)) {
   using(System.Drawing.Graphics graphics=System.Drawing.Graphics.FromImage(bitmap)) {
    IntPtr desktop=Native.GetDC(IntPtr.Zero);if(desktop==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
    IntPtr target=IntPtr.Zero;
    try {target=graphics.GetHdc();if(!Native.BitBlt(target,0,0,width,height,desktop,x,y,0x40CC0020))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
    finally {if(target!=IntPtr.Zero)graphics.ReleaseHdc(target);Native.ReleaseDC(IntPtr.Zero,desktop);}
   }
   bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
  }
 }

 void Update(){
  if(status==null)return;string name=paper.Tool==Tool.Pen?"画笔":paper.Tool==Tool.Highlighter?"荧光笔":paper.Tool==Tool.Eraser?"橡皮擦":paper.Tool==Tool.Text?"文字":"便签";
  bool active=paper.Drawing&&!paper.Hidden;
  caption.Text="ScreenPen 1.2"+(!active?" · 鼠标":paper.Tool==Tool.Text||paper.Tool==Tool.Note?" · "+name:"");
  mode.Content=active?name+"模式 · F8":"鼠标模式 · F8";hide.Content=paper.Hidden?"显示批注":"隐藏批注";
  foreach(KeyValuePair<Tool,Button> pair in toolButtons)pair.Value.Background=pair.Key==paper.Tool&&active?Assets.Brush("#2366B4"):Assets.Brush("#2B3343");
  undoButton.IsEnabled=paper.CanUndo;undoButton.ToolTip=paper.CanUndo?"撤销 · F10 / 右键":"当前没有可撤销操作";
  string hint=paper.Hidden?"批注已隐藏，F8 或 Ctrl+Alt+B 恢复。":!paper.Drawing?"可正常操作电脑，批注保留。":paper.Tool==Tool.Eraser?"拖动局部擦除笔迹；碰到文字/便签会删除整项。":paper.Tool==Tool.Text||paper.Tool==Tool.Note?"点击空白处输入。已有文字/便签：内部点击修改，外围拖动移动。":"按住左键画线；文字/便签内部点击修改，外围拖动移动。";
  status.Text=(message.Length>0?message:hint)+warnings;ToolTip=status.Text;
 }
 static StackPanel Row(Panel parent){StackPanel row=new StackPanel {Orientation=Orientation.Horizontal};parent.Children.Add(row);return row;}
 internal static Button MakeButton(string text,Action action){
  Button b=new Button {Content=text,Margin=new Thickness(3),Padding=new Thickness(10,7,10,7),Background=Assets.Brush("#2B3343"),Foreground=Brushes.White,BorderThickness=new Thickness(0),Cursor=Cursors.Arrow};
  FrameworkElementFactory surface=new FrameworkElementFactory(typeof(Border));surface.Name="Surface";surface.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));surface.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));
  FrameworkElementFactory label=new FrameworkElementFactory(typeof(ContentPresenter));label.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));label.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);label.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);surface.AppendChild(label);
  ControlTemplate template=new ControlTemplate(typeof(Button));template.VisualTree=surface;
  Trigger hover=new Trigger {Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,Assets.Brush("#3C4B63"),"Surface"));template.Triggers.Add(hover);
  Trigger press=new Trigger {Property=Button.IsPressedProperty,Value=true};press.Setters.Add(new Setter(Border.BackgroundProperty,Assets.Brush("#2366B4"),"Surface"));template.Triggers.Add(press);
  Trigger disabled=new Trigger {Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.35));template.Triggers.Add(disabled);b.Template=template;
  b.Click+=delegate{action();};return b;
 }
}
static class Program {
 [STAThread] static void Main(){try{Run();}catch(Exception ex){MessageBox.Show("ScreenPen 无法启动：\n"+ex.Message,"ScreenPen");}}
 static void Run(){
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"--self-test")>=0){Verification.Run();return;}
  bool fresh;using(Mutex mutex=new Mutex(true,"Local\\ScreenPen-45B93D08",out fresh)){
   if(!fresh){MessageBox.Show("ScreenPen 已在运行。请先退出旧版再启动新版。\nCtrl+Alt+H 可显示工具栏。","ScreenPen");return;}
   Application app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};app.DispatcherUnhandledException+=delegate(object s,DispatcherUnhandledExceptionEventArgs e){MessageBox.Show("ScreenPen 遇到错误：\n"+e.Exception.Message,"ScreenPen");e.Handled=true;app.Shutdown(1);};bool test=Array.IndexOf(Environment.GetCommandLineArgs(),"--test")>=0;
   if(test){Window board=new Window {Title="ScreenPen Test Board",Width=1000,Height=650,Left=450,Top=300,Topmost=true,Background=Brushes.White,WindowStyle=WindowStyle.None};board.Show();}Overlay overlay=new Overlay(test);overlay.Show();Toolbar toolbar=new Toolbar(overlay);app.MainWindow=toolbar;toolbar.Show();app.Run();
  }
 }
}
static class Verification {
 internal static void Run(){
  List<string> lines=new List<string>();string output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt");
  try{
   Paper p=new Paper();Mark line=new Mark {Ink=new Stroke(new StylusPointCollection(new[]{new StylusPoint(0,100),new StylusPoint(500,100)}))};p.Marks.Add(line);p.Remember();p.Erase(new Point(250,60),new Point(250,140));Assert(p.Marks.Count==2,"橡皮擦局部切断长线，保留两端",lines);
   p.Undo();Assert(p.Marks.Count==1&&p.Marks[0].Ink.StylusPoints[1].X==500,"撤销恢复完整笔迹",lines);
   p.CommitText(new Point(30,200),null,"中文文字 test",false,24);Mark text=p.Marks[1];Assert(text.Bounds.Width>0,"中文文字布局",lines);
   p.CommitText(new Point(60,250),null,"便签\n第二行",true,20);Mark note=p.Marks[2];Assert(note.Note&&note.Bounds.Width==284,"多行便签布局",lines);
   p.Tool=Tool.Pen;Assert(p.HitText(new Point(100,280))==note,"画笔模式自动识别已有便签",lines);
   Assert(p.CursorAt(new Point(100,280))==Cursors.IBeam,"便签内部使用文字光标",lines);
   Assert(p.CursorAt(new Point(56,280))==Cursors.SizeAll,"便签外围使用四向移动光标",lines);
   Assert(p.CursorAt(new Point(35,205))==Cursors.IBeam,"文字内部使用文字光标",lines);
   Assert(p.CursorAt(new Point(25,205))==Cursors.SizeAll,"文字外围使用移动光标",lines);
   p.OverToolbar=point=>point.X<20;Assert(p.CursorAt(new Point(10,400))==Cursors.Arrow,"进入工具栏区域使用鼠标光标",lines);p.OverToolbar=null;
   p.Tool=Tool.Eraser;Assert(p.CursorAt(new Point(100,280))==Cursors.Cross,"橡皮擦保留文字便签擦除行为",lines);p.Tool=Tool.Pen;
   p.Remember();p.Erase(new Point(70,270),new Point(71,270));Assert(p.Marks.Count==2,"橡皮擦删除便签",lines);p.Undo();Assert(p.Marks.Count==3,"撤销恢复便签",lines);
   p.Clear();Assert(p.Marks.Count==0,"清屏",lines);p.Undo();Assert(p.Marks.Count==3,"清屏可撤销",lines);
   p.Measure(new Size(800,600));p.Arrange(new Rect(0,0,800,600));RenderTargetBitmap b=new RenderTargetBitmap(800,600,96,96,PixelFormats.Pbgra32);b.Render(p);byte[] data=new byte[800*600*4];b.CopyPixels(data,3200,0);Assert(data[200*3200+30*4+3]>0,"画布可渲染",lines);
   p.Hidden=true;p.InvalidateVisual();p.UpdateLayout();RenderTargetBitmap hidden=new RenderTargetBitmap(800,600,96,96,PixelFormats.Pbgra32);hidden.Render(p);byte[] blank=new byte[data.Length];hidden.CopyPixels(blank,3200,0);bool empty=true;for(int i=3;i<blank.Length;i+=4)if(blank[i]!=0){empty=false;break;}Assert(empty,"隐藏批注后完全透明",lines);Assert(p.Marks.Count==3,"隐藏不丢失批注",lines);lines.Add("PASS");File.WriteAllLines(output,lines);
  }catch(Exception ex){lines.Add("FAIL: "+ex);File.WriteAllLines(output,lines);Environment.ExitCode=1;}
 }
 static void Assert(bool value,string label,List<string> lines){if(!value)throw new Exception(label);lines.Add("PASS "+label);}
}
}

