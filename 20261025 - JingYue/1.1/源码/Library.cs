using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Serialization;
namespace JingYue {
 public class PreparedBook {public Book Book;public Document Document;}
 public class LibraryStore {
  public readonly string Root;
  public LibraryStore(string root){Root=root;}
  public Settings Load(){string path=Path.Combine(Root,"settings.xml");foreach(string p in new[]{path,path+".bak"}){try{using(var f=File.OpenRead(p))return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);}catch{}}return new Settings();}
  public void Save(Settings settings){Directory.CreateDirectory(Root);string path=Path.Combine(Root,"settings.xml"),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var f=File.Create(temp))new XmlSerializer(typeof(Settings)).Serialize(f,settings);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public PreparedBook Prepare(string source,string forced){source=Path.GetFullPath(source);string ext=Path.GetExtension(source).ToLowerInvariant();if(!Formats.Extensions.Contains(ext))throw new Exception("不支持 "+ext+"。支持 TXT、MD、EPUB、DOCX、HTML、FB2；MOBI / AZW3、PDF 暂不支持。");string folder=Path.Combine(Root,"Books");Directory.CreateDirectory(folder);string id,path;
   using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read)){if(input.Length>250L*1024*1024)throw new Exception("文件超过 250 MB，请先拆分后阅读。");using(var hash=SHA256.Create())id=BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();path=Path.Combine(folder,id+ext);if(!File.Exists(path)){string temp=path+".tmp";input.Position=0;try{using(var output=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))input.CopyTo(output);File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}}
   var doc=Document.Read(path,forced);return new PreparedBook{Document=doc,Book=new Book{Id=id,Path=path,SourcePath=source,Title=String.IsNullOrWhiteSpace(doc.Title)?Path.GetFileNameWithoutExtension(source):doc.Title,Author=doc.Author,Format=ext.TrimStart('.').ToUpperInvariant(),TotalBlocks=doc.Blocks.Count,Encoding=forced}};
  }
  public Book Register(Settings settings,PreparedBook prepared){var candidate=prepared.Book;var existing=settings.Books.FirstOrDefault(b=>(!String.IsNullOrEmpty(b.Id)&&b.Id==candidate.Id)||(b.SourcePath??b.Path).Equals(candidate.SourcePath,StringComparison.OrdinalIgnoreCase));if(existing==null){settings.Books.Add(candidate);return candidate;}existing.Id=candidate.Id;existing.Path=candidate.Path;existing.SourcePath=candidate.SourcePath;existing.Title=candidate.Title;existing.Author=candidate.Author;existing.Format=candidate.Format;existing.TotalBlocks=candidate.TotalBlocks;existing.Encoding=candidate.Encoding;existing.Position=Math.Max(0,Math.Min(existing.Position,existing.TotalBlocks-1));return existing;}
 }
}
