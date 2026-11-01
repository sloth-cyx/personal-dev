package com.jingyue.reader;
import java.io.*;
import java.nio.charset.*;
import java.nio.file.*;
import java.util.*;
import java.util.zip.*;

public final class ParserTest {
  static File root;static int passed=0;
  static void check(boolean condition,String label){if(!condition)throw new AssertionError(label);passed++;System.out.println("PASS "+label);}
  static File text(String name,String body,String charset)throws Exception{File f=new File(root,name);Files.write(f.toPath(),body.getBytes(charset));return f;}
  static File zip(String name,String... pairs)throws Exception{File f=new File(root,name);try(ZipOutputStream z=new ZipOutputStream(new FileOutputStream(f))){for(int i=0;i<pairs.length;i+=2){z.putNextEntry(new ZipEntry(pairs[i]));z.write(pairs[i+1].getBytes(StandardCharsets.UTF_8));z.closeEntry();}}return f;}
  static BookParser.Result parse(File f,String folder)throws Exception{return BookParser.parse(f,f.getName(),new File(root,folder));}
  public static void main(String[]args)throws Exception{
    if(args.length>0&&args[0].startsWith("pathfile:"))args[0]=new String(Files.readAllBytes(Paths.get(args[0].substring(9))),StandardCharsets.UTF_8).trim();
    root=Files.createTempDirectory("jingyue-parser-").toFile();
    BookParser.Result utf=parse(text("test.txt","第一章 风起\n你好，世界。\n第二章 归来\n一段新的故事。","UTF-8"),"utf");check(utf.count==4&&utf.headings.size()==2,"UTF-8 text and chapter detection");
    BookParser.Result gb=parse(text("gb.txt","第一章 中文编码\n这是一段国标编码的文字。","GB18030"),"gb");check(gb.encoding.equals("GB18030")&&BookParser.read(new File(root,"gb"),1,1).get(0).text.equals("这是一段国标编码的文字。"),"GB18030 detection and exact text");
    BookParser.Result u16=parse(text("utf16.txt","\ufeff第一章 测试\n测试正文","UTF-16LE"),"u16");check(u16.encoding.equals("UTF-16LE")&&u16.count==2,"UTF-16 BOM");
    String original=String.join("",Collections.nCopies(5999,"字"))+"😀"+String.join("",Collections.nCopies(7000,"文"));parse(text("long.txt",original,"UTF-8"),"long");StringBuilder combined=new StringBuilder();for(BookParser.Block b:BookParser.read(new File(root,"long"),0,100))combined.append(b.text);check(original.equals(combined.toString()),"long-line splitting preserves surrogate pairs");
    BookParser.Result md=parse(text("sample.md","# 序章\n**加粗** [文字](https://example.com)\n## 新旅程\n正文","UTF-8"),"md");check(md.headings.size()==2&&BookParser.read(new File(root,"md"),1,1).get(0).text.equals("加粗 文字"),"Markdown headings and readable links");
    File epub=zip("sample.epub","META-INF/container.xml","<container xmlns='urn:oasis:names:tc:opendocument:xmlns:container'><rootfiles><rootfile full-path='OEBPS/book.opf'/></rootfiles></container>","OEBPS/book.opf","<package xmlns='http://www.idpf.org/2007/opf'><metadata><title xmlns='http://purl.org/dc/elements/1.1/'>测试 EPUB</title><creator xmlns='http://purl.org/dc/elements/1.1/'>静阅</creator></metadata><manifest><item id='b' href='body.xhtml'/><item id='nav' href='nav.xhtml' properties='nav'/></manifest><spine><itemref idref='b'/></spine></package>","OEBPS/nav.xhtml","<html xmlns='http://www.w3.org/1999/xhtml' xmlns:epub='http://www.idpf.org/2007/ops'><body><nav epub:type='toc'><ol><li><a href='body.xhtml#start'>自定义标题</a></li><li><a href='body.xhtml#end'>终点</a></li></ol></nav></body></html>","OEBPS/body.xhtml","<html xmlns='http://www.w3.org/1999/xhtml'><head><title>不进入正文</title></head><body><p id='start'>起点</p><p>第二段</p><p id='end'>终点的正文</p></body></html>");
    BookParser.Result er=parse(epub,"epub");check(er.title.equals("测试 EPUB")&&er.author.equals("静阅")&&er.count==3,"EPUB metadata and spine text");check(er.headings.size()==2&&er.headingPositions.get(1)==2,"EPUB3 navigation fragment anchors");
    File docx=zip("sample.docx","word/styles.xml","<w:styles xmlns:w='urn:word'><w:style w:styleId='Custom'><w:name w:val='自定义标题'/><w:basedOn w:val='Base'/></w:style><w:style w:styleId='Base'><w:pPr><w:outlineLvl w:val='0'/></w:pPr></w:style></w:styles>","word/document.xml","<w:document xmlns:w='urn:word'><w:body><w:p><w:pPr><w:pStyle w:val='Custom'/></w:pPr><w:r><w:t>自定义章节</w:t></w:r></w:p><w:p><w:r><w:t>正文内容</w:t></w:r></w:p><w:p><w:pPr><w:pStyle w:val='Custom'/><w:outlineLvl w:val='9'/></w:pPr><w:r><w:t>明确的普通正文</w:t></w:r></w:p></w:body></w:document>");
    BookParser.Result dr=parse(docx,"docx");check(dr.count==3&&dr.headings.size()==1,"DOCX inherited custom heading and body override");
    BookParser.Result fr=parse(text("sample.fb2","<FictionBook xmlns='urn:fb2'><description><title-info><book-title>不重复读取元数据</book-title></title-info></description><body><section><title><p>测试章节</p></title><p>故事开始了。</p></section></body></FictionBook>","UTF-8"),"fb2");check(fr.count==2&&fr.headings.size()==1,"FB2 body and title");
    BookParser.Result hr=parse(text("sample.html","<html><head><script>never execute()</script></head><body><h1>标题</h1><p>完整正文&nbsp;测试</p></body></html>","UTF-8"),"html");check(hr.count==2&&BookParser.read(new File(root,"html"),1,1).get(0).text.contains("测试"),"HTML text and script removal");
    StringBuilder big=new StringBuilder();for(int i=0;i<180000;i++)big.append("这是一段用于测试的长篇小说内容，第 ").append(i).append(" 段。\n");File large=text("large.txt",big.toString(),"UTF-8");long started=System.nanoTime();BookParser.Result lr=parse(large,"large");check(lr.count==180000,"180000 paragraphs streamed and indexed");check(BookParser.read(new File(root,"large"),179999,1).get(0).text.contains("179999"),"random read at end of long book");System.out.println("LONG_IMPORT_MS "+(System.nanoTime()-started)/1000000);
    if(args.length>0&&!args[0].isEmpty()){File sample=new File(args[0]);if(!sample.exists())throw new IOException("Sample path does not exist: "+sample);File[] files=sample.isDirectory()?sample.listFiles():new File[]{sample};int i=0;for(File file:files){if(!file.getName().matches("(?i).*\\.(txt|md|epub|docx|fb2|html)$"))continue;long t=System.nanoTime();BookParser.Result r=parse(file,"real-"+(i++));check(r.count>0,"real sample "+file.getName());System.out.println("SAMPLE "+file.getName()+" blocks="+r.count+" headings="+r.headings.size()+" images="+r.images+" ms="+(System.nanoTime()-t)/1000000);}check(i>0,"sample files were actually tested");}
    System.out.println("ALL "+passed+" CHECKS PASSED");
  }
}
