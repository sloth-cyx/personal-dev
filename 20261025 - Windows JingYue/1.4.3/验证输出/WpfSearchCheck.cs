using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using JingYue;
public static class WpfSearchCheck {
 [STAThread] public static void Main(string[] args){
  var app=new Application();UiTheme.Install(app.Resources);
  foreach(string name in new[]{"Ink","Accent","Muted","SearchFill","ScrollThumb","Line"})app.Resources[name]=Brushes.DarkSlateGray;
  foreach(double size in new[]{14.0,20.0}){
   var box=new TextBox{Width=330,FontSize=size,FontFamily=new FontFamily("Microsoft YaHei UI"),Tag="搜索书名或作者…"};
   var root=new Grid{Width=500,Height=140};root.Children.Add(box);box.VerticalAlignment=VerticalAlignment.Center;
   root.Measure(new Size(500,140));root.Arrange(new Rect(0,0,500,140));root.UpdateLayout();box.ApplyTemplate();root.UpdateLayout();
   var hint=(TextBlock)box.Template.FindName("Hint",box);var host=(ScrollViewer)box.Template.FindName("PART_ContentHost",box);
   var hintOrigin=hint.TranslatePoint(new Point(),box);var emptyCaret=box.GetRectFromCharacterIndex(0);
   box.Text="花嫁 keyword";root.UpdateLayout();var caret=box.GetRectFromCharacterIndex(0);
   double hintTextX=hintOrigin.X+hint.Padding.Left;
   Console.WriteLine("font={0} controlHeight={1:F2} hintText=({2:F2},{3:F2}) caret=({4:F2},{5:F2},{6:F2}) hostMargin={7} hostPadding={8}",size,box.ActualHeight,hintTextX,hintOrigin.Y,caret.X,caret.Y,caret.Height,host.Margin,host.Padding);
   if(args.Length>0){if(caret.IsEmpty||Math.Abs(caret.X-hintTextX)>1.5||Math.Abs(caret.Y-hintOrigin.Y)>1.5)throw new Exception("Caret and placeholder do not align");if(box.ActualHeight>size*1.8+25)throw new Exception("Search field applies padding more than once");if(VisualChildren(box)!=1)throw new Exception("Expected exactly one editing host");}
  }
 }
 static int VisualChildren(DependencyObject obj){int found=obj is ScrollViewer&&((FrameworkElement)obj).Name=="PART_ContentHost"?1:0;for(int i=0;i<VisualTreeHelper.GetChildrenCount(obj);i++)found+=VisualChildren(VisualTreeHelper.GetChild(obj,i));return found;}
}
