using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace JingYue {
 public class DocxHeadings {
  class StyleInfo {public string Name,Parent;public int? Level;}
  Dictionary<string,StyleInfo> styles=new Dictionary<string,StyleInfo>();
  static string Value(XElement e,string attribute){return e==null?null:e.Attributes().Where(a=>a.Name.LocalName==attribute).Select(a=>a.Value).FirstOrDefault();}
  static int? Outline(XElement p){if(p==null)return null;var node=p.Elements().FirstOrDefault(e=>e.Name.LocalName=="outlineLvl");int level;return node!=null&&Int32.TryParse(Value(node,"val"),out level)?(int?)level:null;}
  public DocxHeadings(XDocument xml){if(xml==null)return;foreach(var s in xml.Descendants().Where(e=>e.Name.LocalName=="style"&&Value(e,"type")=="paragraph")){string id=Value(s,"styleId");if(String.IsNullOrEmpty(id))continue;styles[id]=new StyleInfo{Name=Value(s.Elements().FirstOrDefault(e=>e.Name.LocalName=="name"),"val"),Parent=Value(s.Elements().FirstOrDefault(e=>e.Name.LocalName=="basedOn"),"val"),Level=Outline(s.Elements().FirstOrDefault(e=>e.Name.LocalName=="pPr"))};}}
  int? NamedLevel(string name){if(String.IsNullOrEmpty(name))return null;var match=Regex.Match(name,@"^(?:Heading|标题)\s*([1-9])$",RegexOptions.IgnoreCase);return match.Success?(int?)(Int32.Parse(match.Groups[1].Value)-1):null;}
  int? StyleLevel(string id,HashSet<string> visited){if(String.IsNullOrEmpty(id)||!visited.Add(id))return null;StyleInfo s;if(!styles.TryGetValue(id,out s))return NamedLevel(id);if(s.Level.HasValue)return s.Level;return NamedLevel(s.Name)??NamedLevel(id)??StyleLevel(s.Parent,visited);}
  public int Kind(XElement paragraph,string text){var props=paragraph.Elements().FirstOrDefault(e=>e.Name.LocalName=="pPr");int? level=Outline(props);if(!level.HasValue){var style=props==null?null:props.Elements().FirstOrDefault(e=>e.Name.LocalName=="pStyle");level=StyleLevel(Value(style,"val"),new HashSet<string>());}if(level.HasValue)return level.Value>=0&&level.Value<=8?(level.Value<=1?1:2):0;
   return text.Length<=90&&Regex.IsMatch(text.Trim(),@"(?:^|\s)(?:第[零〇一二三四五六七八九十百千万两0-9]+[章节卷部篇回](?:\s.*)?|最终章|终章|序章|楔子|序言|后记)$")?1:0;
  }
 }
}
