package com.jingyue.reader;
import android.content.*;import android.database.*;import android.net.*;import android.os.*;import android.provider.*;import java.io.*;
public final class TestFiles extends ContentProvider {
  public boolean onCreate(){return true;}
  public Cursor query(Uri u,String[]p,String s,String[]a,String o){MatrixCursor c=new MatrixCursor(new String[]{OpenableColumns.DISPLAY_NAME});c.addRow(new Object[]{u.getLastPathSegment()});return c;}
  public String getType(Uri u){return u.getLastPathSegment().endsWith("epub")?"application/epub+zip":"text/plain";}
  public ParcelFileDescriptor openFile(Uri u,String mode)throws FileNotFoundException {String name=u.getLastPathSegment();if(!name.equals("fixture.epub")&&!name.equals("long.txt"))throw new FileNotFoundException();try{File f=new File(getContext().getCacheDir(),name);if(name.equals("long.txt")){try(Writer out=new OutputStreamWriter(new FileOutputStream(f),"UTF-8")){for(int i=0;i<600;i++){if(i%60==0)out.write("第"+(i/60+1)+"章 测试章节\n");out.write("第 "+i+" 段。这是一段测试手机长文分页的文字，山间吹来轻柔的风。我们一起翻过新的一页。\n");}}}else try(InputStream in=getContext().getAssets().open(name);OutputStream out=new FileOutputStream(f)){byte[]b=new byte[8192];int n;while((n=in.read(b))!=-1)out.write(b,0,n);}return ParcelFileDescriptor.open(f,ParcelFileDescriptor.MODE_READ_ONLY);}catch(Exception e){throw new FileNotFoundException(e.toString());}}
  public Uri insert(Uri u,ContentValues v){throw new UnsupportedOperationException();}public int delete(Uri u,String s,String[]a){throw new UnsupportedOperationException();}public int update(Uri u,ContentValues v,String s,String[]a){throw new UnsupportedOperationException();}
}
