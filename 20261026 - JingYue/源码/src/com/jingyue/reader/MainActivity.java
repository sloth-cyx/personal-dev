package com.jingyue.reader;

import android.app.*;
import android.os.*;
import android.content.*;
import android.database.Cursor;
import android.graphics.Color;
import android.net.Uri;
import android.provider.OpenableColumns;
import android.view.*;
import android.webkit.*;
import android.widget.FrameLayout;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.*;
import java.util.*;
import java.util.concurrent.*;

public final class MainActivity extends Activity {
  private WebView web;
  private FrameLayout root;
  private File books;
  private JSONObject state;
  private final Object lock=new Object();
  private final ExecutorService worker=Executors.newSingleThreadExecutor();
  private boolean ready=false;
  private boolean readingActive=false;
  private Intent pending;
  private static final String ORIGIN="https://app.jingyue.local/";
  private static final int IMPORT=100,FONT=101;
  @Override public void onCreate(Bundle saved){
    super.onCreate(saved);books=new File(getFilesDir(),"books");books.mkdirs();
    synchronized(lock){state=loadState();}
    root=new FrameLayout(this);root.setBackgroundColor(Color.rgb(246,239,223));web=new WebView(this);root.addView(web,new FrameLayout.LayoutParams(-1,-1));setContentView(root);
    getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
    if(Build.VERSION.SDK_INT>=30){getWindow().setDecorFitsSystemWindows(false);root.setOnApplyWindowInsetsListener((v,i)->{android.graphics.Insets in=i.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout()|WindowInsets.Type.ime());v.setPadding(in.left,in.top,in.right,in.bottom);return i;});}
    else{root.setFitsSystemWindows(true);}
    WebSettings s=web.getSettings();s.setJavaScriptEnabled(true);s.setDomStorageEnabled(false);s.setAllowFileAccess(false);s.setAllowContentAccess(false);s.setBlockNetworkLoads(true);s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);s.setBuiltInZoomControls(false);s.setTextZoom(100);s.setSupportMultipleWindows(false);
    web.setBackgroundColor(Color.rgb(246,239,223));web.setOverScrollMode(View.OVER_SCROLL_NEVER);
    web.addJavascriptInterface(new Bridge(),"Native");
    web.setWebViewClient(new WebViewClient(){
      @Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest req){return true;}
      @Override public WebResourceResponse shouldInterceptRequest(WebView v,WebResourceRequest req){
        Uri uri=req.getUrl();if(!"https".equals(uri.getScheme())||!"app.jingyue.local".equals(uri.getHost()))return blocked();
        try{
          String p=uri.getPath();InputStream in;String mime="text/html";
          if(p.equals("/")||p.equals("/index.html")){in=getAssets().open("index.html");}
          else if(p.equals("/app.js")||p.equals("/style.css")){in=getAssets().open(p.substring(1));mime=p.endsWith("js")?"application/javascript":"text/css";}
          else if(p.equals("/LXGWWenKai-Regular.ttf")){in=getAssets().open("LXGWWenKai-Regular.ttf");mime="font/ttf";}
          else if(p.equals("/font")){in=new FileInputStream(new File(getFilesDir(),"reading-font"));mime="font/ttf";}
          else if(p.matches("/book/[a-f0-9]{64}/(?:image-[0-9]+|fb2-[0-9]+)\\.(png|jpg|jpeg|gif|webp|bmp)")){
            String[] parts=p.split("/");File f=new File(new File(books,parts[2]),parts[3]);in=new FileInputStream(f);mime=p.endsWith("png")?"image/png":p.endsWith("webp")?"image/webp":p.endsWith("gif")?"image/gif":"image/jpeg";
          }else return blocked();
          Map<String,String> h=new HashMap<>();h.put("Content-Security-Policy","default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self'; font-src 'self'; connect-src 'none'; base-uri 'none'; frame-src 'none'");h.put("Cache-Control","no-store");
          return new WebResourceResponse(mime,mime.startsWith("text")||mime.contains("javascript")?"UTF-8":null,200,"OK",h,in);
        }catch(Exception e){return blocked();}
      }
    });
    web.setWebChromeClient(new WebChromeClient());web.loadUrl(ORIGIN);pending=getIntent();
    if(Build.VERSION.SDK_INT>=33)getOnBackInvokedDispatcher().registerOnBackInvokedCallback(android.window.OnBackInvokedDispatcher.PRIORITY_DEFAULT,()->onBackPressed());
  }
  private static WebResourceResponse blocked(){return new WebResourceResponse("text/plain","UTF-8",404,"Not Found",null,new ByteArrayInputStream(new byte[0]));}
  private JSONObject loadState(){
    for(String n:new String[]{"library.json","library.json.bak"})try{return new JSONObject(read(new File(getFilesDir(),n)));}catch(Exception ignored){}
    try{return new JSONObject("{\"books\":[],\"settings\":{\"theme\":\"warm\",\"size\":21,\"line\":1.9,\"mode\":\"scroll\",\"font\":\"wenkai\",\"awake\":false}}");}catch(Exception e){throw new RuntimeException(e);}
  }
  private static String read(File f)throws IOException {try(InputStream in=new FileInputStream(f);ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1)out.write(b,0,n);return out.toString("UTF-8");}}
  private void persist()throws Exception {
    File current=new File(getFilesDir(),"library.json"),temp=new File(getFilesDir(),"library.json.tmp"),bak=new File(getFilesDir(),"library.json.bak");
    try(FileOutputStream out=new FileOutputStream(temp)){out.write(state.toString().getBytes(StandardCharsets.UTF_8));out.getFD().sync();}
    if(current.exists()){try{new JSONObject(read(current));copy(current,bak);}catch(JSONException invalid){/* Keep the known-good backup after recovery. */}}if(!temp.renameTo(current))throw new IOException("阅读记录保存失败，存储空间可能不足");
  }
  private static void copy(File a,File b)throws IOException {try(InputStream in=new FileInputStream(a);OutputStream out=new FileOutputStream(b)){byte[] buf=new byte[16384];int n;while((n=in.read(buf))!=-1)out.write(buf,0,n);}}
  private JSONObject book(String id)throws Exception {JSONArray list=state.getJSONArray("books");for(int i=0;i<list.length();i++){JSONObject b=list.getJSONObject(i);if(id.equals(b.getString("id")))return b;}throw new IOException("书籍不存在");}
  private void emit(String event,Object data){runOnUiThread(()->{if(!isFinishing()&&!isDestroyed())web.evaluateJavascript("window.receive("+JSONObject.quote(event)+","+(data instanceof JSONObject||data instanceof JSONArray?data.toString():JSONObject.quote(String.valueOf(data)))+")",null);});}
  private static JSONObject error(Exception e){JSONObject j=new JSONObject();try{j.put("error",e.getMessage()==null?"操作失败":e.getMessage());}catch(Exception ignored){}return j;}
  private final class Bridge {
    @JavascriptInterface public String library(){synchronized(lock){return state.toString();}}
    @JavascriptInterface public void ready(){ready=true;runOnUiThread(()->handleIntent(pending));}
    @JavascriptInterface public String fontLicense(){try(InputStream in=getAssets().open("OFL-WenKai.txt");ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[4096];int n;while((n=in.read(b))!=-1)out.write(b,0,n);return out.toString("UTF-8");}catch(Exception e){return "LXGW WenKai — SIL Open Font License 1.1";}}
    @JavascriptInterface public void pickBooks(){runOnUiThread(()->{Intent i=new Intent(Intent.ACTION_OPEN_DOCUMENT);i.addCategory(Intent.CATEGORY_OPENABLE);i.setType("*/*");i.putExtra(Intent.EXTRA_ALLOW_MULTIPLE,true);startActivityForResult(i,IMPORT);});}
    @JavascriptInterface public void reading(boolean active){runOnUiThread(()->{readingActive=active;try{applySystem(state.getJSONObject("settings"));}catch(Exception ignored){}});}
    @JavascriptInterface public void pickFont(){runOnUiThread(()->{Intent i=new Intent(Intent.ACTION_OPEN_DOCUMENT);i.addCategory(Intent.CATEGORY_OPENABLE);i.setType("*/*");startActivityForResult(i,FONT);});}
    @JavascriptInterface public String range(String id,int start,int count){
      try{synchronized(lock){book(id);}JSONArray a=new JSONArray();for(BookParser.Block b:BookParser.read(new File(books,id),Math.max(0,start),Math.max(1,Math.min(80,count)))){JSONObject item=new JSONObject().put("type",b.type).put("text",b.text);if(b.type.equals("image")){android.graphics.BitmapFactory.Options o=new android.graphics.BitmapFactory.Options();o.inJustDecodeBounds=true;android.graphics.BitmapFactory.decodeFile(new File(new File(books,id),b.text).getPath(),o);item.put("width",Math.max(1,o.outWidth)).put("height",Math.max(1,o.outHeight));}a.put(item);}return a.toString();}catch(Exception e){return error(e).toString();}
    }
    @JavascriptInterface public void search(String id,String query){worker.execute(()->{try{if(query.trim().isEmpty())return;int total;synchronized(lock){total=book(id).getInt("count");}JSONArray found=new JSONArray();for(int start=0;start<total&&found.length()<100;start+=100){List<BookParser.Block> blocks=BookParser.read(new File(books,id),start,100);for(int j=0;j<blocks.size()&&found.length()<100;j++){BookParser.Block b=blocks.get(j);int hit=b.text.toLowerCase(Locale.ROOT).indexOf(query.toLowerCase(Locale.ROOT));if(!b.type.equals("image")&&hit>=0){int left=Math.max(0,hit-25),right=Math.min(b.text.length(),hit+query.length()+70);found.put(new JSONObject().put("position",start+j).put("text",(left>0?"…":"")+b.text.substring(left,right)+(right<b.text.length()?"…":"")));}}}emit("search",new JSONObject().put("id",id).put("query",query).put("results",found));}catch(Exception e){emit("error",e.getMessage());}});}
    @JavascriptInterface public void update(String id,String patch){synchronized(lock){try{JSONObject b=book(id),p=new JSONObject(patch);for(String k:new String[]{"position","fraction","marks","lastRead"})if(p.has(k))b.put(k,p.get(k));persist();}catch(Exception e){emit("error",e.getMessage());}}}
    @JavascriptInterface public void settings(String json){synchronized(lock){try{JSONObject j=new JSONObject(json);state.put("settings",j);persist();runOnUiThread(()->applySystem(j));}catch(Exception e){emit("error",e.getMessage());}}}
    @JavascriptInterface public void systemTheme(String theme){runOnUiThread(()->{JSONObject s=new JSONObject();try{s.put("theme",theme);s.put("awake",state.getJSONObject("settings").optBoolean("awake"));}catch(Exception ignored){}applySystem(s);});}
    @JavascriptInterface public void deleteBook(String id){
      synchronized(lock){try{JSONObject existing=book(id);JSONArray old=state.getJSONArray("books"),next=new JSONArray();for(int i=0;i<old.length();i++)if(!old.getJSONObject(i).getString("id").equals(id))next.put(old.get(i));state.put("books",next);try{persist();}catch(Exception e){state.put("books",old);throw e;}deleteTree(new File(books,existing.getString("id")));emit("library",state);}catch(Exception e){emit("error",e.getMessage());}}
    }
    @JavascriptInterface public void close(){runOnUiThread(()->finish());}
  }
  private void applySystem(JSONObject s){boolean dark=s.optString("theme").equals("night");int bg=Color.parseColor(dark?"#172320":s.optString("theme").equals("paper")?"#f9faf6":"#f6efdf");root.setBackgroundColor(bg);web.setBackgroundColor(bg);getWindow().setStatusBarColor(bg);getWindow().setNavigationBarColor(bg);if(Build.VERSION.SDK_INT>=30){WindowInsetsController c=getWindow().getInsetsController();if(c!=null)c.setSystemBarsAppearance(dark?0:WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS|WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS,WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS|WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS);}else getWindow().getDecorView().setSystemUiVisibility(dark?0:View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);if(s.optBoolean("awake")&&readingActive)getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);else getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);}
  private String displayName(Uri uri){try(Cursor c=getContentResolver().query(uri,new String[]{OpenableColumns.DISPLAY_NAME},null,null,null)){if(c!=null&&c.moveToFirst())return c.getString(0);}catch(Exception ignored){}return "导入书籍.txt";}
  private void importUris(List<Uri> uris){
    emit("busy","正在整理书籍…");worker.execute(()->{int imported=0,duplicates=0;List<String> errors=new ArrayList<>();
      for(Uri uri:uris){File staging=null;try{
        String name=displayName(uri),ext=name.substring(name.lastIndexOf('.')+1).toLowerCase(Locale.ROOT);
        if(!Arrays.asList("txt","md","markdown","epub","docx","html","htm","fb2").contains(ext))throw new IOException(name+"：不支持此格式");
        staging=new File(getCacheDir(),"import-"+UUID.randomUUID());staging.mkdirs();File source=new File(staging,"source."+ext);MessageDigest digest=MessageDigest.getInstance("SHA-256");
        try(InputStream in=getContentResolver().openInputStream(uri);OutputStream out=new FileOutputStream(source)){if(in==null)throw new IOException("无法读取这个文件");byte[] buf=new byte[32768];long size=0;int n;while((n=in.read(buf))!=-1){size+=n;if(size>250L*1024*1024)throw new IOException("单本书请控制在 250 MB 内");digest.update(buf,0,n);out.write(buf,0,n);}}
        StringBuilder hex=new StringBuilder();for(byte b:digest.digest())hex.append(String.format(Locale.ROOT,"%02x",b&255));String id=hex.toString();
        synchronized(lock){boolean exists=false;JSONArray a=state.getJSONArray("books");for(int i=0;i<a.length();i++)if(a.getJSONObject(i).getString("id").equals(id))exists=true;if(exists){duplicates++;continue;}}
        emit("busy","正在导入："+name);File parsed=new File(staging,"parsed");BookParser.Result r=BookParser.parse(source,name,parsed);optimizeImages(parsed);File target=new File(books,id);
        if(target.exists())deleteTree(target);if(!parsed.renameTo(target))throw new IOException("无法保存书籍，请检查剩余空间");
        JSONObject b=new JSONObject().put("id",id).put("title",r.title).put("author",r.author).put("format",ext.toUpperCase(Locale.ROOT)).put("count",r.count).put("chars",r.chars).put("images",r.images).put("encoding",r.encoding).put("position",0).put("fraction",0).put("marks",new JSONArray()).put("lastRead",0).put("added",System.currentTimeMillis());
        JSONArray toc=new JSONArray();for(int i=0;i<r.headings.size();i++)toc.put(new JSONObject().put("title",r.headings.get(i)).put("position",r.headingPositions.get(i)));b.put("toc",toc);
        synchronized(lock){JSONArray a=state.getJSONArray("books");a.put(b);try{persist();}catch(Exception e){a.remove(a.length()-1);deleteTree(target);throw e;}}imported++;
      }catch(Exception e){errors.add(e.getMessage()==null?"导入失败":e.getMessage());}finally{if(staging!=null)deleteTree(staging);}}
      synchronized(lock){emit("library",state);}emit("importDone","已导入 "+imported+" 本"+(duplicates>0?" · "+duplicates+" 本已在书库":"")+(errors.isEmpty()?"":"\n"+String.join("\n",errors)));
    });
  }
  private static void deleteTree(File f){if(f.isDirectory()){File[] a=f.listFiles();if(a!=null)for(File c:a)deleteTree(c);}f.delete();}
  private static void optimizeImages(File dir){File[] files=dir.listFiles();if(files==null)return;for(File f:files){if(!f.getName().matches("(?:image-|fb2-).*"))continue;try{android.graphics.BitmapFactory.Options size=new android.graphics.BitmapFactory.Options();size.inJustDecodeBounds=true;android.graphics.BitmapFactory.decodeFile(f.getPath(),size);if(Math.max(size.outWidth,size.outHeight)<=2400)continue;android.graphics.BitmapFactory.Options opts=new android.graphics.BitmapFactory.Options();opts.inSampleSize=1;while(Math.max(size.outWidth,size.outHeight)/opts.inSampleSize>2400)opts.inSampleSize*=2;android.graphics.Bitmap bitmap=android.graphics.BitmapFactory.decodeFile(f.getPath(),opts);if(bitmap==null)continue;File temp=new File(dir,f.getName()+".tmp");try(OutputStream out=new FileOutputStream(temp)){bitmap.compress(bitmap.hasAlpha()?android.graphics.Bitmap.CompressFormat.PNG:android.graphics.Bitmap.CompressFormat.JPEG,90,out);}finally{bitmap.recycle();}if(!temp.renameTo(f))temp.delete();}catch(Exception ignored){}}}
  @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(result!=RESULT_OK||data==null)return;
    if(request==IMPORT){List<Uri> list=new ArrayList<>();if(data.getClipData()!=null)for(int i=0;i<data.getClipData().getItemCount();i++)list.add(data.getClipData().getItemAt(i).getUri());else if(data.getData()!=null)list.add(data.getData());importUris(list);}
    else if(request==FONT&&data.getData()!=null){Uri uri=data.getData();worker.execute(()->{try{String name=displayName(uri);if(!name.toLowerCase(Locale.ROOT).matches(".*\\.(ttf|otf)$"))throw new IOException("请选择 TTF 或 OTF 字体文件");File temp=new File(getCacheDir(),"font.tmp");try(InputStream in=getContentResolver().openInputStream(uri);OutputStream out=new FileOutputStream(temp)){byte[] b=new byte[16384];int n,total=0;while((n=in.read(b))!=-1){total+=n;if(total>40*1024*1024)throw new IOException("字体文件请小于 40 MB");out.write(b,0,n);}}android.graphics.Typeface.createFromFile(temp);copy(temp,new File(getFilesDir(),"reading-font"));temp.delete();synchronized(lock){state.getJSONObject("settings").put("font","custom").put("fontName",name);persist();emit("font",state.getJSONObject("settings"));}}catch(Exception e){emit("error",e.getMessage());}});}
  }
  @Override protected void onNewIntent(Intent intent){super.onNewIntent(intent);setIntent(intent);pending=intent;if(ready)handleIntent(intent);}
  private void handleIntent(Intent i){if(i!=null&&Intent.ACTION_SEND.equals(i.getAction())){Uri u=i.getParcelableExtra(Intent.EXTRA_STREAM);if(u!=null)importUris(Collections.singletonList(u));}pending=null;}
  @Override public void onBackPressed(){web.evaluateJavascript("window.androidBack()",null);}
  @Override protected void onPause(){web.evaluateJavascript("window.savePosition&&window.savePosition()",null);super.onPause();}
  @Override protected void onDestroy(){super.onDestroy();worker.shutdown();web.removeJavascriptInterface("Native");web.destroy();}
}
