using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JingYue {
 public class ReadingImage {
  public string ArchivePath,EntryName,Description,Error; public int Anchor,PixelWidth,PixelHeight; public bool After;
 }
 public partial class Document {
  public List<ReadingImage> Images=new List<ReadingImage>();
  public void AttachImages(){if(Images.Count>0&&Blocks.Count==0)Blocks.Add(new Paragraph{Text="",Index=0});foreach(var image in Images){int index=Math.Max(0,Math.Min(image.Anchor,Blocks.Count-1));if(image.After||image.Anchor>=Blocks.Count)Blocks[index].AfterImages.Add(image);else Blocks[index].BeforeImages.Add(image);}}
 }
 public static class ReadingImageCache {
  class Cached {public BitmapSource Source;public long Bytes;}
  static readonly object gate=new object();static readonly Dictionary<string,Cached> items=new Dictionary<string,Cached>();static readonly LinkedList<string> order=new LinkedList<string>();static long bytes;
  public static BitmapSource Load(ReadingImage image){if(image.Error!=null)throw new InvalidDataException(image.Error);string key=image.ArchivePath+"|"+image.EntryName;lock(gate){Cached c;if(items.TryGetValue(key,out c)){order.Remove(key);order.AddLast(key);return c.Source;}}
   BitmapImage result;using(var zip=ZipFile.OpenRead(image.ArchivePath)){var entry=zip.GetEntry(image.EntryName);if(entry==null)throw new InvalidDataException("书中缺少这张图片。");if(entry.Length>32L*1024*1024)throw new InvalidDataException("单张图片超过 32 MB。");using(var stream=new MemoryStream()){using(var input=entry.Open())input.CopyTo(stream);stream.Position=0;var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);var frame=decoder.Frames[0];int w=frame.PixelWidth,h=frame.PixelHeight;if(w<1||h<1||w>100000||h>100000||(long)w*h>160000000)throw new InvalidDataException("图片尺寸过大。");double scale=Math.Min(1,Math.Min(1600.0/w,8192.0/h));stream.Position=0;result=new BitmapImage();result.BeginInit();result.CacheOption=BitmapCacheOption.OnLoad;result.DecodePixelWidth=Math.Max(1,(int)(w*scale));result.StreamSource=stream;result.EndInit();result.Freeze();}}
   lock(gate){Cached old;if(items.TryGetValue(key,out old))return old.Source;long size=(long)result.PixelWidth*result.PixelHeight*4;while(bytes+size>64L*1024*1024&&order.Count>0){string first=order.First.Value;bytes-=items[first].Bytes;items.Remove(first);order.RemoveFirst();}if(size<=64L*1024*1024){items[key]=new Cached{Source=result,Bytes=size};order.AddLast(key);bytes+=size;}}return result;
  }
 }
 public class ReadingImageView:Border {
  ReadingImage asset;Image picture;TextBlock caption;bool active;int generation;double maxHeight;
  public ReadingImageView(ReadingImage image,double maxHeight=650){asset=image;this.maxHeight=maxHeight;Margin=new Thickness(8,18,8,22);Padding=new Thickness(12);CornerRadius=new CornerRadius(10);SetResourceReference(BackgroundProperty,"Code");var stack=new StackPanel();Child=stack;picture=new Image{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Center};stack.Children.Add(picture);caption=new TextBlock{Text="正在载入图片…",FontSize=12,Height=20,TextTrimming=TextTrimming.CharacterEllipsis,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,8,0,0)};caption.SetResourceReference(TextBlock.ForegroundProperty,"Muted");stack.Children.Add(caption);Loaded+=(s,e)=>Start();Unloaded+=(s,e)=>{active=false;generation++;picture.Source=null;};}
  protected override Size MeasureOverride(Size available){double width=Double.IsInfinity(available.Width)?820:available.Width+Margin.Left+Margin.Right;picture.Height=ImageLayout.Height(asset,width,maxHeight);return base.MeasureOverride(available);}
  async void Start(){if(active)return;active=true;int ticket=++generation;try{var source=await Task.Run(()=>ReadingImageCache.Load(asset));if(!active||ticket!=generation)return;picture.Source=source;caption.Text=asset.Description??"";}catch(Exception ex){if(active&&ticket==generation)caption.Text="图片未显示："+ex.Message;}}
 }
 public class ReaderBlock:StackPanel {
  public ReaderBlock(){DataContextChanged+=(s,e)=>Render();}
  void Render(){Children.Clear();var unit=DataContext as ReadingUnit;if(unit!=null){if(unit.Image!=null)Children.Add(new ReadingImageView(unit.Image,unit.MaxImageHeight));else if(unit.Paragraph!=null)Children.Add(new ReaderText{DataContext=unit.Paragraph});return;}var p=DataContext as Paragraph;if(p==null)return;foreach(var image in p.BeforeImages)Children.Add(new ReadingImageView(image));if(!String.IsNullOrEmpty(p.Text))Children.Add(new ReaderText());foreach(var image in p.AfterImages)Children.Add(new ReadingImageView(image));}
 }
}
