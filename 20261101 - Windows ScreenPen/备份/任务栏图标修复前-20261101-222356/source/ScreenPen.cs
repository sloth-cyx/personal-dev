using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace ScreenPen {
static class Native {
 internal static void Debug(string text) { if(Array.IndexOf(Environment.GetCommandLineArgs(),"--test")>=0) System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-events.log"),text+Environment.NewLine); }
 [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h, int n);
 [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr h,int n,int v);
 [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint m,uint k);
 [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
}
class Stroke { internal List<Point> Points = new List<Point>(); internal Color Color; internal double Width; internal bool Highlight; }
class Paper : FrameworkElement {
 internal List<Stroke> Strokes = new List<Stroke>();
 internal Stroke Current;
 internal Color Ink = Colors.Red;
 internal double Size = 4;
 internal bool Highlight, Drawing = true;
 internal void Finish() { Current = null; if(IsMouseCaptured) ReleaseMouseCapture(); }
 internal void Undo() { Finish(); if(Strokes.Count>0) Strokes.RemoveAt(Strokes.Count-1); InvalidateVisual(); }
 internal void Clear() { Finish(); Strokes.Clear(); InvalidateVisual(); }
 protected override void OnMouseDown(MouseButtonEventArgs e) {
  if(!Drawing) return;
  if(e.ChangedButton == MouseButton.Right) { Undo(); return; }
  if(e.ChangedButton != MouseButton.Left) return;
  Current = new Stroke { Color=Ink, Width=Highlight?Size*5:Size, Highlight=Highlight };
  Current.Points.Add(e.GetPosition(this)); Strokes.Add(Current); CaptureMouse(); InvalidateVisual(); e.Handled=true;
 }
 protected override void OnMouseMove(MouseEventArgs e) { if(Current!=null && e.LeftButton==MouseButtonState.Pressed) { Current.Points.Add(e.GetPosition(this)); InvalidateVisual(); } }
 protected override void OnMouseUp(MouseButtonEventArgs e) { if(e.ChangedButton==MouseButton.Left) Finish(); }
 protected override void OnLostMouseCapture(MouseEventArgs e) { Current=null; base.OnLostMouseCapture(e); }
 protected override void OnRender(DrawingContext dc) {
  dc.DrawRectangle(Drawing ? new SolidColorBrush(Color.FromArgb(1,0,0,0)) : Brushes.Transparent,null,new Rect(RenderSize));
  foreach(Stroke s in Strokes) {
   Brush brush=new SolidColorBrush(s.Color); Pen pen=new Pen(brush,s.Width) {StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
   dc.PushOpacity(s.Highlight?0.35:1);
   if(s.Points.Count==1) dc.DrawEllipse(brush,null,s.Points[0],s.Width/2,s.Width/2);
   else { StreamGeometry g=new StreamGeometry(); using(StreamGeometryContext c=g.Open()) { c.BeginFigure(s.Points[0],false,false); c.PolyLineTo(s.Points.GetRange(1,s.Points.Count-1),true,false); } g.Freeze(); dc.DrawGeometry(null,pen,g); }
   dc.Pop();
  }
 }
}
class Overlay : Window {
 internal Paper Paper = new Paper(); internal IntPtr Handle;
 internal Overlay() {
  Title="ScreenPen Canvas"; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false;
  Content=Paper; Cursor=Cursors.Pen;
  Loaded += delegate { Handle=new WindowInteropHelper(this).EnsureHandle(); if(!ShowInTaskbar) Native.SetWindowLong(Handle,-20,Native.GetWindowLong(Handle,-20)|0x80); };
  SetBounds();
 }
 internal void SetBounds() { Left=SystemParameters.VirtualScreenLeft; Top=SystemParameters.VirtualScreenTop; Width=SystemParameters.VirtualScreenWidth; Height=SystemParameters.VirtualScreenHeight; }
 internal void SetDrawing(bool value) { Paper.Finish(); Paper.Drawing=value; int s=Native.GetWindowLong(Handle,-20); Native.SetWindowLong(Handle,-20,value?(s&~0x20):(s|0x20)); Cursor=value?Cursors.Pen:Cursors.Arrow; Paper.InvalidateVisual(); }
}
class Toolbar : Window {
 internal Overlay Overlay; Button mode,pen,high; TextBlock status; IntPtr handle; HwndSourceHook messageHook; List<int> registered=new List<int>(); bool quitting,hotkeysReady; string hotkeyWarnings="";
 internal Toolbar(Overlay overlay) {
  using(var iconStream=typeof(Toolbar).Assembly.GetManifestResourceStream("AppIcon.ico")) { var image=new System.Windows.Media.Imaging.BitmapImage(); image.BeginInit(); image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; image.StreamSource=iconStream; image.EndInit(); image.Freeze(); Icon=image; }
  Overlay=overlay; Title="ScreenPen 屏幕画笔"; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; SizeToContent=SizeToContent.WidthAndHeight; Topmost=true; Background=new SolidColorBrush(Color.FromRgb(24,29,41)); Foreground=Brushes.White; FontFamily=new FontFamily("Microsoft YaHei UI"); FontSize=13;
  Left=SystemParameters.WorkArea.Left+24; Top=SystemParameters.WorkArea.Top+24;
  StackPanel root=new StackPanel {Margin=new Thickness(12)}; Content=root;
  DockPanel header=new DockPanel(); TextBlock title=new TextBlock { Text="✎  ScreenPen", FontWeight=FontWeights.Bold,FontSize=17,Margin=new Thickness(3,0,20,8),Cursor=Cursors.SizeAll }; title.MouseLeftButtonDown+=delegate { try { DragMove(); } catch(InvalidOperationException) {} };
  Button exit=Button("退出",delegate { Close(); }); exit.Width=58; DockPanel.SetDock(exit,Dock.Right); header.Children.Add(exit); header.Children.Add(title); root.Children.Add(header);
  StackPanel tools=Row(root); mode=Button("画笔模式 · F8",delegate { Toggle(); }); tools.Children.Add(mode);
  pen=Button("画笔",delegate { Overlay.Paper.Highlight=false; Update(); }); tools.Children.Add(pen);
  high=Button("荧光笔",delegate { Overlay.Paper.Highlight=true; if(Overlay.Paper.Ink==Colors.Red) Overlay.Paper.Ink=Colors.Yellow; Update(); }); tools.Children.Add(high);
  tools.Children.Add(Button("撤销",delegate { Overlay.Paper.Undo(); })); tools.Children.Add(Button("清屏 · F9",delegate { Overlay.Paper.Clear(); }));
  StackPanel palette=Row(root); string[] colors={"#FF4545","#FFD83D","#39D98A","#38BDF8","#C084FC","#FFFFFF"}; string[] names={"红色","黄色","绿色","蓝色","紫色","白色"};
  Overlay.Paper.Ink=(Color)ColorConverter.ConvertFromString(colors[0]);
  for(int i=0;i<colors.Length;i++) { Color color=(Color)ColorConverter.ConvertFromString(colors[i]); Button b=Button("●",delegate { Overlay.Paper.Ink=color; Update(); }); b.Foreground=new SolidColorBrush(color); b.FontSize=24; b.Width=36; b.ToolTip=names[i]; System.Windows.Automation.AutomationProperties.SetName(b,names[i]); palette.Children.Add(b); }
  palette.Children.Add(new TextBlock { Text=" 粗细",VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(8,0,5,0) });
  ComboBox widths=new ComboBox { Width=62,Margin=new Thickness(3,5,3,5),VerticalContentAlignment=VerticalAlignment.Center }; foreach(int size in new int[]{2,4,7,11}) widths.Items.Add(size); widths.SelectedItem=4; widths.SelectionChanged+=delegate { Overlay.Paper.Size=(int)widths.SelectedItem; }; palette.Children.Add(widths);
  status=new TextBlock {Margin=new Thickness(4,8,4,2),Foreground=Brushes.LightGray}; root.Children.Add(status);
  root.Children.Add(new TextBlock {Text="F8 切换鼠标/画笔   F9 清屏   F10 / 右键撤销\nCtrl+Alt+H 隐藏/显示工具栏",FontSize=11,Foreground=Brushes.LightSlateGray,Margin=new Thickness(4,5,4,0)});
  Loaded+=delegate { if(hotkeysReady)return; hotkeysReady=true; handle=new WindowInteropHelper(this).EnsureHandle(); messageHook=new HwndSourceHook(Hook); HwndSource.FromHwnd(handle).AddHook(messageHook); Register(1,0x4000,0x77,"F8"); Register(2,0x4000,0x78,"F9"); Register(3,0x4000,0x79,"F10"); Register(4,0x4003,0x48,"Ctrl+Alt+H"); };
  PreviewKeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape && Overlay.Paper.Drawing) { Toggle(); e.Handled=true; } };
  Closing+=delegate { quitting=true; foreach(int id in registered) Native.UnregisterHotKey(handle,id); SystemEvents.DisplaySettingsChanged-=DisplayChanged; Overlay.Close(); Application.Current.Shutdown(); };
  SystemEvents.DisplaySettingsChanged+=DisplayChanged;
  Update();
 }
 void DisplayChanged(object sender,EventArgs e) { Dispatcher.BeginInvoke(new Action(delegate { if(quitting)return; Overlay.Paper.Clear(); Overlay.SetBounds(); Left=SystemParameters.WorkArea.Left+24; Top=SystemParameters.WorkArea.Top+24; })); }
 void Register(int id,uint mods,uint key,string label) { if(Native.RegisterHotKey(handle,id,mods,key)) { registered.Add(id); Native.Debug("Registered "+label+" hwnd="+handle); } else { hotkeyWarnings+="\n"+label+" 被占用，请用工具栏按钮"; Update(); } }
 IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) { if(msg==0x312 || msg==0x100) Native.Debug("Message "+msg+" w="+w); if(msg==0x312) { switch(w.ToInt32()) {case 1:Toggle();break;case 2:Overlay.Paper.Clear();break;case 3:Overlay.Paper.Undo();break;case 4:if(IsVisible) Hide();else {Show();Activate();}break;} handled=true; } return IntPtr.Zero; }
 void Toggle() { Overlay.SetDrawing(!Overlay.Paper.Drawing); Update(); }
 void Update() { mode.Content=Overlay.Paper.Drawing?"画笔模式 · F8":"鼠标模式 · F8"; mode.Background=Overlay.Paper.Drawing?new SolidColorBrush(Color.FromRgb(35,102,180)):new SolidColorBrush(Color.FromRgb(42,57,70)); pen.FontWeight=Overlay.Paper.Highlight?FontWeights.Normal:FontWeights.Bold; high.FontWeight=Overlay.Paper.Highlight?FontWeights.Bold:FontWeights.Normal; status.Text=(Overlay.Paper.Drawing?"● 可直接在屏幕上画；F8 恢复正常点击":"● 可正常操作电脑，屏幕批注保留")+hotkeyWarnings; status.Foreground=new SolidColorBrush(Overlay.Paper.Ink); }
 static StackPanel Row(Panel parent) { StackPanel p=new StackPanel {Orientation=Orientation.Horizontal}; parent.Children.Add(p); return p; }
 static Button Button(string text,Action action) { Button b=new Button {Content=text,Margin=new Thickness(3),Padding=new Thickness(9,6,9,6),Background=new SolidColorBrush(Color.FromRgb(43,51,67)),Foreground=Brushes.White,BorderThickness=new Thickness(0),Cursor=Cursors.Hand}; b.Click+=delegate {action();}; return b; }
}
static class Program {
 [STAThread] static void Main() {
  try { Run(); } catch(Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.log"),ex.ToString()); MessageBox.Show(ex.ToString(),"ScreenPen 启动失败"); }
 }
 static void Run() {
  bool fresh; using(Mutex mutex=new Mutex(true,"Local\\ScreenPen-45B93D08",out fresh)) {
   if(!fresh) { MessageBox.Show("ScreenPen 已经启动，请用 F8 切换画笔，或 Ctrl+Alt+H 显示工具栏。","ScreenPen"); return; }
   Application app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
   app.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) { MessageBox.Show("ScreenPen 遇到错误并将退出：\n"+e.Exception.Message,"ScreenPen"); e.Handled=true; app.Shutdown(1); };
   bool test=Array.IndexOf(Environment.GetCommandLineArgs(),"--test")>=0;
   Window board=null;
   if(test) { board=new Window {Title="ScreenPen Test Board",Width=1000,Height=650,Left=450,Top=280,Background=Brushes.White,WindowStyle=WindowStyle.None}; board.Show(); }
   Overlay overlay=new Overlay();
   if(test) { overlay.ShowInTaskbar=true; overlay.Left=450; overlay.Top=280; overlay.Width=1000; overlay.Height=650; }
   overlay.Show(); Toolbar toolbar=new Toolbar(overlay); app.MainWindow=toolbar; toolbar.Show(); app.Run();
  }
 }
}
}
