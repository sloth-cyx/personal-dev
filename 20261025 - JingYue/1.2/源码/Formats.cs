using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Net;

namespace JingYue {
 public static class Formats {
  public static readonly string[] Extensions={".txt",".md",".markdown",".epub",".docx",".html",".htm",".fb2"};
  public const string Filter="支持的书籍|*.txt;*.md;*.markdown;*.epub;*.docx;*.html;*.htm;*.fb2|电子书 EPUB|*.epub|文本与 Markdown|*.txt;*.md;*.markdown|Word 文档|*.docx|网页与 FictionBook|*.html;*.htm;*.fb2";
  const long MaxExpanded=300L*1024*1024;
  public static Document Read(string path){if(new FileInfo(path).Length>250L*1024*1024)throw new Exception("文件超过 250 MB，请先拆分后阅读。");string ext=Path.GetExtension(path).ToLowerInvariant();Document d;
   if(ext==".epub")d=Epub(path);else if(ext==".docx")d=Docx(path);else if(ext==".fb2")d=Fb2(path);else if(ext==".html"||ext==".htm"){d=new Document{EncodingName="HTML"};Html(File.ReadAllText(path),d);}else throw new Exception("暂不支持此格式。可导入 TXT、Markdown、EPUB、DOCX、HTML、FB2。MOBI / AZW3、PDF 暂不支持。");
   if(d.Blocks.Count==0)throw new Exception("没有找到可阅读的文字。图片型、扫描型或受 DRM 保护的书籍暂不支持。");return d;
  }
  internal static void Add(Document d,string text,int kind){text=(text??"").Trim();if(text.Length==0)return;d.Characters+=text.Length;for(int i=0;i<text.Length;){int n=Math.Min(1200,text.Length-i);if(i+n<text.Length&&Char.IsHighSurrogate(text[i+n-1]))n--;var p=new Paragraph{Text=text.Substring(i,n),Kind=kind,Index=d.Blocks.Count};d.Blocks.Add(p);if((kind==1||kind==2)&&i==0)d.Chapters.Add(p);i+=n;}}
  static XmlReaderSettings XmlOptions(){return new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null,MaxCharactersInDocument=MaxExpanded,IgnoreComments=true};}
  static XDocument Xml(Stream stream){using(var r=XmlReader.Create(stream,XmlOptions()))return XDocument.Load(r);}
  static ZipArchive Archive(string path){var z=ZipFile.OpenRead(path);try{if(z.Entries.Count>20000||z.Entries.Sum(e=>e.Length)>MaxExpanded)throw new Exception("书籍解压后的内容过大，已停止导入。");return z;}catch{z.Dispose();throw;}}
  static ZipArchiveEntry Entry(ZipArchive z,string name){var e=z.GetEntry(name);if(e==null)throw new Exception("书籍缺少内容文件："+name);return e;}
  static string Resolve(string file,string href){var uri=new Uri(new Uri("https://local.invalid/"+file),href);if(uri.Host!="local.invalid")throw new Exception("书籍包含外部章节地址，无法作为离线书籍读取。");return Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));}
  static string Attr(XElement e,string name){return (string)e.Attribute(name)??"";}
  static string Target(string file,string href){var u=new Uri(new Uri("https://local.invalid/"+file),href);return Resolve(file,href)+Uri.UnescapeDataString(u.Fragment);}
  static Document Epub(string path){var d=new Document{EncodingName="EPUB · 流式重排"};using(var zip=Archive(path)){
   XDocument container;using(var s=Entry(zip,"META-INF/container.xml").Open())container=Xml(s);var rf=container.Descendants().FirstOrDefault(x=>x.Name.LocalName=="rootfile");if(rf==null)throw new Exception("EPUB 缺少书籍目录。");string opfPath=Attr(rf,"full-path");XDocument opf;using(var s=Entry(zip,opfPath).Open())opf=Xml(s);
   d.Title=opf.Descendants().Where(x=>x.Name.LocalName=="title").Select(x=>x.Value).FirstOrDefault();d.Author=String.Join(" / ",opf.Descendants().Where(x=>x.Name.LocalName=="creator").Select(x=>x.Value));
   var encryption=zip.GetEntry("META-INF/encryption.xml");if(encryption!=null){XDocument ex;using(var s=encryption.Open())ex=Xml(s);if(ex.Descendants().Where(x=>x.Name.LocalName=="CipherReference").Any(x=>Regex.IsMatch(Attr(x,"URI"),@"\.(x?html?|xml)(\?|$)",RegexOptions.IgnoreCase)))throw new Exception("这本 EPUB 的正文受 DRM 加密保护，暂不支持。");}
   var manifest=opf.Descendants().Where(x=>x.Name.LocalName=="item"&&x.Attribute("id")!=null).ToDictionary(x=>Attr(x,"id"),x=>x);
   var toc=new List<Tuple<string,string,int>>();var navItem=manifest.Values.FirstOrDefault(x=>Attr(x,"properties").Split(' ').Contains("nav"));var ncx=manifest.Values.FirstOrDefault(x=>Attr(x,"media-type")=="application/x-dtbncx+xml");
   // EPUB 3 navigation and EPUB 2 NCX provide directories even without h1/h2 markup.
   try{var tocItem=navItem??ncx;if(tocItem!=null){string tocPath=Resolve(opfPath,Attr(tocItem,"href"));XDocument tocXml;using(var s=Entry(zip,tocPath).Open())tocXml=Xml(s);if(navItem!=null){var section=tocXml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="nav"&&x.Attributes().Any(a=>a.Name.LocalName=="type"&&a.Value.Split(' ').Contains("toc")))??tocXml.Descendants().FirstOrDefault(x=>x.Name.LocalName=="nav");if(section!=null)foreach(var a in section.Descendants().Where(x=>x.Name.LocalName=="a"&&x.Attribute("href")!=null))toc.Add(Tuple.Create(Target(tocPath,Attr(a,"href")),Spaces.Replace(a.Value," ").Trim(),a.Ancestors().Count(x=>x.Name.LocalName=="ol")>1?2:1));}else foreach(var p in tocXml.Descendants().Where(x=>x.Name.LocalName=="navPoint")){var c=p.Elements().FirstOrDefault(x=>x.Name.LocalName=="content");var l=p.Elements().FirstOrDefault(x=>x.Name.LocalName=="navLabel");if(c!=null&&l!=null)toc.Add(Tuple.Create(Target(tocPath,Attr(c,"src")),Spaces.Replace(l.Value," ").Trim(),p.Ancestors().Any(x=>x.Name.LocalName=="navPoint")?2:1));}}}catch(XmlException){/* Fall back to headings in malformed navigation documents. */}
   var locations=new Dictionary<string,int>();foreach(var itemref in opf.Descendants().Where(x=>x.Name.LocalName=="itemref")){if(Attr(itemref,"linear")=="no")continue;XElement item;if(!manifest.TryGetValue(Attr(itemref,"idref"),out item))throw new Exception("EPUB 阅读顺序引用了缺失的章节。");if(Attr(item,"properties").Split(' ').Contains("nav"))continue;string media=Attr(item,"media-type");if(media!="application/xhtml+xml"&&media!="text/html")continue;string contentPath=Resolve(opfPath,Attr(item,"href"));locations[contentPath]=d.Blocks.Count;using(var s=Entry(zip,contentPath).Open())using(var r=new StreamReader(s,Encoding.UTF8,true)){var anchors=Html(r.ReadToEnd(),d);foreach(var a in anchors)locations[contentPath+"#"+a.Key]=a.Value;}}
   var chapters=new List<Paragraph>();foreach(var t in toc){int index;if(locations.TryGetValue(t.Item1,out index)&&index<d.Blocks.Count&&!String.IsNullOrWhiteSpace(t.Item2))chapters.Add(new Paragraph{Text=t.Item2,Index=index,Kind=t.Item3});}if(chapters.Count>0)d.Chapters=chapters.GroupBy(c=>c.Index).Select(g=>g.First()).OrderBy(c=>c.Index).ToList();
  }return d;}
  static Document Docx(string path){var d=new Document{EncodingName="DOCX · 正文重排"};using(var z=Archive(path)){
   var props=z.GetEntry("docProps/core.xml");if(props!=null){using(var s=props.Open()){var x=Xml(s);d.Title=x.Descendants().Where(e=>e.Name.LocalName=="title").Select(e=>e.Value).FirstOrDefault();d.Author=x.Descendants().Where(e=>e.Name.LocalName=="creator").Select(e=>e.Value).FirstOrDefault();}}
   using(var s=Entry(z,"word/document.xml").Open())using(var r=XmlReader.Create(s,XmlOptions())){while(r.Read()){if(r.NodeType!=XmlNodeType.Element||r.LocalName!="p")continue;XElement p;using(var sub=r.ReadSubtree())p=XElement.Load(sub);var text=new StringBuilder();foreach(var e in p.Descendants()){if(e.Name.LocalName=="t")text.Append(e.Value);else if(e.Name.LocalName=="tab")text.Append("  ");else if(e.Name.LocalName=="br")text.Append('\n');}var style=p.Descendants().FirstOrDefault(e=>e.Name.LocalName=="pStyle");string styleName=style==null?"":String.Join("",style.Attributes().Where(a=>a.Name.LocalName=="val").Select(a=>a.Value));int kind=Regex.IsMatch(styleName,@"^(Heading|标题)\s*[12]$",RegexOptions.IgnoreCase)?1:Regex.IsMatch(styleName,@"^(Heading|标题)\s*[3-6]$",RegexOptions.IgnoreCase)?2:0;string t=text.ToString();if(kind==0&&Regex.IsMatch(t,@"^第[零〇一二三四五六七八九十百千万两0-9]+[章节卷部篇回].{0,70}$"))kind=1;Add(d,t,kind);}}
  }return d;}
  static Document Fb2(string path){var d=new Document{EncodingName="FB2 · 正文重排"};bool body=false;using(var s=File.OpenRead(path))using(var r=XmlReader.Create(s,XmlOptions())){while(r.Read()){
   if(r.NodeType==XmlNodeType.Element&&r.LocalName=="book-title"&&!body)d.Title=r.ReadElementContentAsString();
   if(r.LocalName=="body")body=r.NodeType==XmlNodeType.Element;
   if(!body||r.NodeType!=XmlNodeType.Element)continue;if(r.LocalName=="title"||r.LocalName=="p"||r.LocalName=="subtitle"){int kind=r.LocalName=="title"?1:r.LocalName=="subtitle"?2:0;XElement e;using(var sub=r.ReadSubtree())e=XElement.Load(sub);Add(d,String.Join(" ",e.DescendantNodes().OfType<XText>().Select(t=>t.Value)),kind);}
  }}return d;}
  static readonly Regex HtmlTokens=new Regex(@"<!--[\s\S]*?-->|<![^>]*>|<[^>]*>|[^<]+",RegexOptions.Compiled);
  static readonly Regex Spaces=new Regex(@"\s+",RegexOptions.Compiled);
  static Dictionary<string,int> Html(string html,Document d){var anchors=new Dictionary<string,int>();var text=new StringBuilder();int kind=0;bool pre=false;string skip=null;Action flush=()=>{string t=WebUtility.HtmlDecode(text.ToString());if(!pre)t=Spaces.Replace(t," ");Add(d,t,kind);text.Clear();};foreach(Match m in HtmlTokens.Matches(html)){string token=m.Value;if(token[0]!='<'){if(skip==null)text.Append(token);continue;}if(token.StartsWith("<!"))continue;var tag=Regex.Match(token,@"^<\s*(/?)\s*([\w:-]+)");if(!tag.Success)continue;bool close=tag.Groups[1].Value=="/";string name=tag.Groups[2].Value.ToLowerInvariant();if(skip!=null){if(close&&name==skip)skip=null;continue;}if(!close&&(name=="script"||name=="style"||name=="head"||name=="svg")){skip=name;continue;}
   bool heading=Regex.IsMatch(name,@"^h[1-6]$");bool block=heading||new[]{"p","div","section","article","li","blockquote","pre","tr","body"}.Contains(name);if(name=="br"){flush();continue;}if(name=="td"||name=="th"){text.Append("  ");continue;}if(block){flush();if(close){kind=0;if(name=="pre")pre=false;}else{kind=heading?(name=="h1"||name=="h2"?1:2):name=="blockquote"?3:name=="pre"?4:name=="li"?5:0;if(name=="pre")pre=true;if(name=="li")text.Append("•  ");}}if(!close){var id=Regex.Match(token,@"\b(?:id|name)\s*=\s*['"+"\""+@"]([^'"+"\""+@"]+)['"+"\""+@"]",RegexOptions.IgnoreCase);if(id.Success)anchors[WebUtility.HtmlDecode(id.Groups[1].Value)]=d.Blocks.Count;}
  }flush();return anchors;}
 }
}
