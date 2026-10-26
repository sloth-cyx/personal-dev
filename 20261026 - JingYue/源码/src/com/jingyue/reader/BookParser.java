package com.jingyue.reader;

import java.io.*;
import java.nio.*;
import java.nio.charset.*;
import java.security.MessageDigest;
import java.util.*;
import java.util.regex.*;
import java.util.zip.*;
import javax.xml.parsers.*;
import org.xml.sax.*;
import org.xml.sax.helpers.DefaultHandler;

/** Streaming import. Blocks stay on disk; the reader only asks for a small range. */
public final class BookParser {
  public static final class Block {
    public String type, text;
    public Block(String t,String s){type=t;text=s;}
  }
  public static final class Result {
    public String title="",author="",encoding="UTF-8";
    public int count,images;
    public long chars;
    public final List<String> headings=new ArrayList<>();
    public final List<Integer> headingPositions=new ArrayList<>();
  }
  private final File dir;
  private final DataOutputStream data,index;
  private long offset=0;
  private final Result result=new Result();
  private ZipFile zip;
  private final Map<String,String> imageNames=new HashMap<>();
  private final Map<String,String> navigation=new LinkedHashMap<>();
  private String chapterPath="";
  private long extractedBytes=0;
  private static final Pattern CHAPTER=Pattern.compile("^(第[零〇一二三四五六七八九十百千万两0-9]+[章回卷节部篇集]|序章|序言|前言|楔子|尾声|后记|终章|最终章|Chapter\\s+\\d+).{0,65}$",Pattern.CASE_INSENSITIVE);
  private BookParser(File d)throws IOException {
    dir=d; d.mkdirs();
    data=new DataOutputStream(new BufferedOutputStream(new FileOutputStream(new File(d,"blocks.bin"))));
    index=new DataOutputStream(new BufferedOutputStream(new FileOutputStream(new File(d,"index.bin"))));
  }
  public static Result parse(File source,String name,File out)throws Exception {
    BookParser p=new BookParser(out);
    p.result.title=name.replaceFirst("(?i)\\.[^.]+$","");
    String ext=name.substring(name.lastIndexOf('.')+1).toLowerCase(Locale.ROOT);
    try {
      if(ext.equals("epub"))p.epub(source);
      else if(ext.equals("docx"))p.docx(source);
      else if(ext.equals("fb2"))p.fb2(source);
      else if(ext.equals("html")||ext.equals("htm"))p.html(readText(source),"",null);
      else if(ext.equals("txt")||ext.equals("md")||ext.equals("markdown"))p.text(source,!ext.equals("txt"));
      else throw new IOException("暂不支持这个格式，请选择 TXT、MD、EPUB、DOCX、HTML 或 FB2。");
      if(p.result.count==0)throw new IOException("没有找到可阅读的正文。加密或 DRM 电子书暂不支持。");
      return p.result;
    } finally {p.data.close();p.index.close();if(p.zip!=null)p.zip.close();}
  }
  private void add(String type,String raw)throws IOException {
    String s=raw.replace('\u00a0',' ').trim();if(s.isEmpty())return;
    // Bound every text block, including pathological single-line novels.
    if(!type.equals("image")&&s.length()>6000){for(int from=0;from<s.length();){int to=Math.min(from+6000,s.length());if(to<s.length()&&Character.isHighSurrogate(s.charAt(to-1)))to--;add(from==0?type:"text",s.substring(from,to));from=to;}return;}
    if(type.equals("text")&&CHAPTER.matcher(s).matches())type="heading";
    if(type.equals("heading"))heading(s);
    if(type.equals("image"))result.images++;else result.chars+=s.length();
    byte[] b=s.getBytes(StandardCharsets.UTF_8);
    index.writeLong(offset);data.writeByte(type.equals("heading")?1:type.equals("image")?2:0);data.writeInt(b.length);data.write(b);
    offset+=5+b.length;result.count++;
  }
  private void heading(String title){int n=result.headingPositions.size();if(n>0&&result.headingPositions.get(n-1)==result.count)return;result.headings.add(title);result.headingPositions.add(result.count);}
  public static List<Block> read(File dir,int start,int amount)throws IOException {
    List<Block> list=new ArrayList<>();
    try(RandomAccessFile idx=new RandomAccessFile(new File(dir,"index.bin"),"r");RandomAccessFile dat=new RandomAccessFile(new File(dir,"blocks.bin"),"r")){
      int total=(int)(idx.length()/8);start=Math.max(0,start);amount=Math.min(100,amount);
      if(start>=total)return list;idx.seek(start*8L);dat.seek(idx.readLong());
      for(int i=start;i<Math.min(total,start+amount);i++){
        int t=dat.readUnsignedByte(),n=dat.readInt();if(n<0||n>1024*1024)throw new IOException("书籍缓存损坏，请重新导入");
        byte[] b=new byte[n];dat.readFully(b);list.add(new Block(t==1?"heading":t==2?"image":"text",new String(b,StandardCharsets.UTF_8)));
      }
    }return list;
  }
  private static String encoding(File f)throws IOException {
    byte[] b=new byte[65536];int n;try(InputStream in=new FileInputStream(f)){n=in.read(b);}if(n<0)return "UTF-8";
    if(n>=2&&(b[0]&255)==255&&(b[1]&255)==254)return "UTF-16LE";
    if(n>=2&&(b[0]&255)==254&&(b[1]&255)==255)return "UTF-16BE";
    if(n>=3&&(b[0]&255)==239&&(b[1]&255)==187&&(b[2]&255)==191)return "UTF-8";
    CharsetDecoder dec=StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT);
    return dec.decode(ByteBuffer.wrap(b,0,n),CharBuffer.allocate(n+1),false).isError()?"GB18030":"UTF-8";
  }
  private static String readText(File f)throws IOException {
    if(f.length()>32*1024*1024)throw new IOException("HTML 文件过大，请转换为 TXT 或 EPUB。");
    try(Reader r=new InputStreamReader(new FileInputStream(f),encoding(f))){StringBuilder s=new StringBuilder();char[] b=new char[8192];int n;while((n=r.read(b))!=-1)s.append(b,0,n);return s.toString().replace("\ufeff","");}
  }
  private void text(File f,boolean md)throws IOException {
    result.encoding=encoding(f);
    try(BufferedReader r=new BufferedReader(new InputStreamReader(new FileInputStream(f),result.encoding))){
      // Read bounded pieces rather than readLine(), which can allocate a whole malformed book.
      StringBuilder line=new StringBuilder();char[] b=new char[8192];int n;
      while((n=r.read(b))!=-1)for(int i=0;i<n;i++){
        char c=b[i];if(c=='\n'||(line.length()>=6000&&!Character.isLowSurrogate(c))){emitLine(line.toString(),md);line.setLength(0);}if(c!='\r'&&c!='\n'&&c!='\ufeff')line.append(c);
      }emitLine(line.toString(),md);
    }
  }
  private void emitLine(String s,boolean md)throws IOException {
    String t="text";
    if(md&&s.matches("^#{1,6}\\s+.*")){s=s.replaceFirst("^#{1,6}\\s+","");t="heading";}
    if(md)s=s.replaceAll("!\\[([^]]*)\\]\\([^)]*\\)","[图片：$1]").replaceAll("\\[([^]]+)\\]\\([^)]*\\)","$1").replaceAll("\\*\\*(.+?)\\*\\*","$1");
    add(t,s);
  }
  private static void xml(InputStream in,DefaultHandler h)throws Exception {
    SAXParserFactory fac=SAXParserFactory.newInstance();fac.setNamespaceAware(true);
    try{fac.setFeature(javax.xml.XMLConstants.FEATURE_SECURE_PROCESSING,true);}catch(Exception ignored){}
    try{fac.setFeature("http://xml.org/sax/features/external-general-entities",false);fac.setFeature("http://xml.org/sax/features/external-parameter-entities",false);}catch(Exception ignored){}
    XMLReader reader=fac.newSAXParser().getXMLReader();reader.setEntityResolver((publicId,systemId)->new InputSource(new StringReader("")));reader.setContentHandler(h);reader.setErrorHandler(h);try{reader.parse(new InputSource(in));}finally{in.close();}
  }
  private static String attr(Attributes a,String key){for(int i=0;i<a.getLength();i++)if(a.getLocalName(i).equals(key)||a.getQName(i).equals(key))return a.getValue(i);return "";}
  private static String norm(String base,String path){
    path=path.replace('\\','/').split("#")[0].split("\\?")[0];try{path=java.net.URLDecoder.decode(path.replace("+","%2B"),"UTF-8");}catch(Exception ignored){}
    if(path.contains(":")||path.startsWith("/"))return "";
    Deque<String> parts=new ArrayDeque<>();for(String p:(base+path).split("/")){if(p.equals("..")){if(parts.isEmpty())return "";parts.removeLast();}else if(!p.isEmpty()&&!p.equals("."))parts.add(p);}return String.join("/",parts);
  }
  private static String parent(String s){int p=s.lastIndexOf('/');return p<0?"":s.substring(0,p+1);}
  private byte[] entry(String name,int limit)throws IOException {
    ZipEntry e=zip.getEntry(name);if(e==null)throw new IOException("电子书缺少内容："+name);if(e.getSize()>limit)throw new IOException("书籍内容超过安全大小限制");
    try(InputStream in=zip.getInputStream(e);ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] buf=new byte[8192];int n;while((n=in.read(buf))!=-1){if(out.size()+n>limit)throw new IOException("书籍内容过大");out.write(buf,0,n);}return out.toByteArray();}
  }
  private String image(String name)throws Exception {
    if(name.isEmpty())return "";if(imageNames.containsKey(name))return imageNames.get(name);
    String ext=name.replaceFirst("^.*\\.","").toLowerCase(Locale.ROOT);
    if(!Arrays.asList("png","jpg","jpeg","gif","webp","bmp").contains(ext))return "";
    String safe="image-"+imageNames.size()+"."+ext;byte[] b=entry(name,32*1024*1024);extractedBytes+=b.length;if(extractedBytes>300L*1024*1024)throw new IOException("插图总大小超过 300 MB");
    try(OutputStream out=new FileOutputStream(new File(dir,safe))){out.write(b);}imageNames.put(name,safe);return safe;
  }
  private void epub(File f)throws Exception {
    zip=new ZipFile(f);checkArchive();final String[] opf={""};
    xml(new ByteArrayInputStream(entry("META-INF/container.xml",1024*1024)),new DefaultHandler(){public void startElement(String u,String l,String q,Attributes a){if(l.equals("rootfile")&&opf[0].isEmpty())opf[0]=attr(a,"full-path");}});
    if(opf[0].isEmpty())throw new IOException("EPUB 缺少内容清单");
    final Map<String,String> manifest=new HashMap<>();final List<String> spine=new ArrayList<>();final StringBuilder title=new StringBuilder(),author=new StringBuilder();final String[] nav={""},ncx={""};
    xml(new ByteArrayInputStream(entry(opf[0],4*1024*1024)),new DefaultHandler(){String tag="";public void startElement(String u,String l,String q,Attributes a){tag=l;if(l.equals("item")){String path=norm(parent(opf[0]),attr(a,"href"));manifest.put(attr(a,"id"),path);if(Arrays.asList(attr(a,"properties").split("\\s+")).contains("nav"))nav[0]=path;if(attr(a,"media-type").equals("application/x-dtbncx+xml"))ncx[0]=path;}if(l.equals("itemref")&&!attr(a,"linear").equals("no"))spine.add(attr(a,"idref"));}public void characters(char[]c,int s,int n){if(tag.equals("title"))title.append(c,s,n);if(tag.equals("creator"))author.append(c,s,n);}public void endElement(String u,String l,String q){tag="";}});
    if(title.length()>0)result.title=title.toString().trim();result.author=author.toString().trim();
    if(!nav[0].isEmpty())try{xml(new ByteArrayInputStream(entry(nav[0],4*1024*1024)),new DefaultHandler(){String href="";StringBuilder label;int inToc=0;public void startElement(String u,String l,String q,Attributes a){if(l.equals("nav")&&(attr(a,"type").contains("toc")||attr(a,"role").equals("doc-toc")))inToc++;if(inToc>0&&l.equals("a")){href=navKey(parent(nav[0]),attr(a,"href"));label=new StringBuilder();}}public void characters(char[]c,int s,int n){if(label!=null)label.append(c,s,n);}public void endElement(String u,String l,String q){if(l.equals("a")&&label!=null){navigation.put(href,label.toString().trim());label=null;}if(l.equals("nav"))inToc=0;}});}catch(Exception ignored){}
    if(navigation.isEmpty()&&!ncx[0].isEmpty())try{xml(new ByteArrayInputStream(entry(ncx[0],4*1024*1024)),new DefaultHandler(){Deque<StringBuilder> labels=new ArrayDeque<>();boolean text=false;public void startElement(String u,String l,String q,Attributes a){if(l.equals("navPoint"))labels.push(new StringBuilder());if(l.equals("text"))text=true;if(l.equals("content")&&!labels.isEmpty())navigation.put(navKey(parent(ncx[0]),attr(a,"src")),labels.peek().toString().trim());}public void characters(char[]c,int s,int n){if(text&&!labels.isEmpty())labels.peek().append(c,s,n);}public void endElement(String u,String l,String q){if(l.equals("text"))text=false;if(l.equals("navPoint")&&!labels.isEmpty())labels.pop();}});}catch(Exception ignored){}
    for(String id:spine){String name=manifest.get(id);if(name!=null&&!name.isEmpty()){chapterPath=name;if(navigation.containsKey(name))heading(navigation.get(name));html(new String(entry(name,32*1024*1024),StandardCharsets.UTF_8),parent(name),zip);}}
  }
  private static String navKey(String base,String href){String path=norm(base,href);int n=href.indexOf('#');return path+(n>=0?href.substring(n):"");}
  private void checkArchive()throws IOException{long total=0;int count=0;Enumeration<? extends ZipEntry> es=zip.entries();while(es.hasMoreElements()){ZipEntry e=es.nextElement();total+=Math.max(0,e.getSize());if(total>350L*1024*1024||++count>30000)throw new IOException("电子书解压内容过大，请先分卷再导入");}}
  private void html(String source,String base,ZipFile archive)throws Exception {
    source=source.replaceFirst("^\\ufeff","").replaceAll("(?is)<!DOCTYPE[^>]*(?:\\[[\\s\\S]*?\\]\\s*)?>","").replace("&nbsp;","&#160;");
    final String input=source;int before=result.count;
    // Validate first: malformed HTML must not leave a half-imported document.
    try{xml(new ByteArrayInputStream(source.getBytes(StandardCharsets.UTF_8)),new DefaultHandler());}catch(Exception invalid){plainHtml(input);return;}
    try {xml(new ByteArrayInputStream(source.getBytes(StandardCharsets.UTF_8)),new DefaultHandler(){
      StringBuilder text=new StringBuilder();String type="text";int skip=0;
      void flush()throws SAXException{try{add(type,text.toString());text.setLength(0);}catch(IOException e){throw new SAXException(e);}}
      public void startElement(String u,String l,String q,Attributes a)throws SAXException {
        if(l.equals("script")||l.equals("style")||l.equals("head")||l.equals("nav")){skip++;return;}if(skip>0)return;
        String id=attr(a,"id");if(!id.isEmpty()&&navigation.containsKey(chapterPath+"#"+id)){flush();heading(navigation.get(chapterPath+"#"+id));}
        if(l.matches("p|div|h[1-6]|li|blockquote|section|br")){flush();type=l.matches("h[1-6]")?"heading":"text";}
        if(l.equals("img")||l.equals("image")){flush();String src=attr(a,l.equals("img")?"src":"href");if(archive!=null)try{String path=image(norm(base,src));if(!path.isEmpty())add("image",path);else add("text","[暂不支持的插图格式]");}catch(Exception e){try{add("text","[插图无法读取]");}catch(Exception ex){throw new SAXException(ex);}}}
      }
      public void characters(char[]c,int s,int n){if(skip==0)text.append(c,s,n);}
      public void endElement(String u,String l,String q)throws SAXException{if(l.equals("script")||l.equals("style")||l.equals("head")||l.equals("nav")){skip=Math.max(0,skip-1);return;}if(skip==0&&l.matches("p|div|h[1-6]|li|blockquote|section|body")){flush();type="text";}}
      public void endDocument()throws SAXException{flush();}
    });}catch(Exception e){
      throw e;
    }
  }
  private void plainHtml(String input)throws IOException{String s=input.replaceAll("(?is)<(script|style|head|nav)\\b[^>]*>.*?</\\1>","").replaceAll("(?is)</?(p|div|h[1-6]|li|br|section)\\b[^>]*>","\n").replaceAll("(?s)<[^>]+>","");for(String line:s.split("\n"))add("text",entities(line));}
  private static String entities(String s){return s.replace("&nbsp;"," ").replace("&lt;","<").replace("&gt;",">").replace("&quot;","\"").replace("&apos;","'").replace("&amp;","&");}
  private static class Style{String based="",name="";int outline=-1;}
  private void docx(File f)throws Exception {
    zip=new ZipFile(f);checkArchive();final Map<String,Style> styles=new HashMap<>();final Map<String,String> rels=new HashMap<>();
    if(zip.getEntry("word/styles.xml")!=null)xml(new ByteArrayInputStream(entry("word/styles.xml",8*1024*1024)),new DefaultHandler(){Style current;public void startElement(String u,String l,String q,Attributes a){if(l.equals("style")){current=new Style();styles.put(attr(a,"styleId"),current);}else if(current!=null){if(l.equals("name"))current.name=attr(a,"val");if(l.equals("basedOn"))current.based=attr(a,"val");if(l.equals("outlineLvl"))try{current.outline=Integer.parseInt(attr(a,"val"));}catch(Exception ignored){}}}public void endElement(String u,String l,String q){if(l.equals("style"))current=null;}});
    if(zip.getEntry("word/_rels/document.xml.rels")!=null)xml(new ByteArrayInputStream(entry("word/_rels/document.xml.rels",4*1024*1024)),new DefaultHandler(){public void startElement(String u,String l,String q,Attributes a){if(l.equals("Relationship")&&!attr(a,"TargetMode").equals("External"))rels.put(attr(a,"Id"),norm("word/",attr(a,"Target")));}});
    xml(zip.getInputStream(zip.getEntry("word/document.xml")),new DefaultHandler(){
      StringBuilder text=new StringBuilder();List<String> pics=new ArrayList<>();String style="";int outline=-1,pdepth=0,hidden=0;boolean inText=false;
      public void startElement(String u,String l,String q,Attributes a)throws SAXException {
        if(l.equals("del")){hidden++;return;}if(hidden>0)return;
        if(l.equals("p")){if(pdepth++==0){text.setLength(0);pics.clear();style="";outline=-1;}}
        if(l.equals("pStyle"))style=attr(a,"val");if(l.equals("outlineLvl"))try{outline=Integer.parseInt(attr(a,"val"));}catch(Exception ignored){}
        if(l.equals("t"))inText=true;if(l.equals("tab"))text.append("  ");if(l.equals("br"))text.append('\n');
        if(l.equals("blip")||l.equals("imagedata")){String id=attr(a,l.equals("blip")?"embed":"id");String target=rels.get(id);if(target!=null)try{String path=image(target);if(!path.isEmpty())pics.add(path);}catch(Exception ignored){}}
      }
      public void characters(char[]c,int s,int n){if(inText&&hidden==0)text.append(c,s,n);}
      public void endElement(String u,String l,String q)throws SAXException {
        if(l.equals("del")){hidden=Math.max(0,hidden-1);return;}if(hidden>0)return;if(l.equals("t"))inText=false;
        if(l.equals("p")&&--pdepth==0)try{
          boolean heading=outline>=0&&outline<9;Set<String> seen=new HashSet<>();String key=style;
          while(outline<0&&!heading&&!key.isEmpty()&&seen.add(key)){Style st=styles.get(key);if(st==null){heading=key.matches("(?i)(heading|标题)[ _]?[1-9]");break;}if(st.outline==9)break;heading=(st.outline>=0&&st.outline<9)||st.name.matches("(?i)(heading|标题)[ _]?[1-9]");key=st.based;}
          add(heading?"heading":"text",text.toString());for(String pic:pics)add("image",pic);
        }catch(Exception e){throw new SAXException(e);}
      }
    });
  }
  private void fb2(File f)throws Exception {
    // FB2 binary images are decoded only once during import.
    final Map<String,String> binaries=new HashMap<>();
    xml(new FileInputStream(f),new DefaultHandler(){String id="",mime="";StringBuilder b;public void startElement(String u,String l,String q,Attributes a){if(l.equals("binary")){id=attr(a,"id");mime=attr(a,"content-type");b=new StringBuilder();}}public void characters(char[]c,int s,int n){if(b!=null&&b.length()<44*1024*1024)b.append(c,s,n);}public void endElement(String u,String l,String q)throws SAXException{if(l.equals("binary")&&b!=null)try{String ext=mime.contains("png")?"png":mime.contains("webp")?"webp":"jpg";String name="fb2-"+binaries.size()+"."+ext;byte[] data=Base64.getMimeDecoder().decode(b.toString());if(data.length<=32*1024*1024){try(OutputStream out=new FileOutputStream(new File(dir,name))){out.write(data);}binaries.put(id,name);}b=null;}catch(Exception e){b=null;}}});
    xml(new FileInputStream(f),new DefaultHandler(){boolean body=false;int title=0;StringBuilder s=new StringBuilder();public void startElement(String u,String l,String q,Attributes a)throws SAXException{if(l.equals("body"))body=true;if(!body)return;if(l.equals("title"))title++;if(l.equals("p"))s.setLength(0);if(l.equals("image")){String path=binaries.get(attr(a,"href").replaceFirst("^#",""));if(path!=null)try{add("image",path);}catch(Exception e){throw new SAXException(e);}}}public void characters(char[]c,int st,int n){if(body)s.append(c,st,n);}public void endElement(String u,String l,String q)throws SAXException{if(body&&l.equals("p"))try{add(title>0?"heading":"text",s.toString());s.setLength(0);}catch(Exception e){throw new SAXException(e);}if(l.equals("title"))title=Math.max(0,title-1);if(l.equals("body"))body=false;}});
  }
}
