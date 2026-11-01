using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class Engine {
    public static string Hash(Stream stream, string algorithm, CancellationToken token) {
        using (HashAlgorithm h = HashAlgorithm.Create(algorithm)) {
            byte[] buffer = new byte[1024 * 1024]; int n;
            while ((n = stream.Read(buffer, 0, buffer.Length)) > 0) {
                token.ThrowIfCancellationRequested(); h.TransformBlock(buffer, 0, n, buffer, 0);
            }
            token.ThrowIfCancellationRequested(); h.TransformFinalBlock(new byte[0], 0, 0);
            return BitConverter.ToString(h.Hash).Replace("-", "").ToLowerInvariant();
        }
    }
    public static string FileHash(string path, string algorithm, CancellationToken token) {
        using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return Hash(s, algorithm, token);
    }
    public static Dictionary<string,string> Entries(string path, CancellationToken token) {
        var result = new Dictionary<string,string>(StringComparer.Ordinal);
        using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var zip = new ZipArchive(s, ZipArchiveMode.Read)) {
            foreach (var e in zip.Entries) {
                token.ThrowIfCancellationRequested();
                if (result.ContainsKey(e.FullName)) throw new InvalidDataException("压缩包含重复路径，无法可靠配对：" + e.FullName);
                using (var data = e.Open()) result.Add(e.FullName, Hash(data, "SHA256", token));
            }
        }
        return result;
    }
    public static string CompareArchives(string a, string b, CancellationToken token) {
        var left = Entries(a, token); var right = Entries(b, token);
        var names = new SortedSet<string>(left.Keys, StringComparer.Ordinal); names.UnionWith(right.Keys);
        int added=0, removed=0, changed=0, same=0; var lines = new System.Text.StringBuilder();
        foreach (string name in names) {
            token.ThrowIfCancellationRequested();
            if (!left.ContainsKey(name)) { added++; lines.AppendLine("[新增 B] " + name); }
            else if (!right.ContainsKey(name)) { removed++; lines.AppendLine("[仅有 A] " + name); }
            else if (left[name] != right[name]) { changed++; lines.AppendLine("[已修改] " + name); }
            else same++;
        }
        return (added+removed+changed == 0 ? "内部内容相同" : "内部内容不同") + "\r\n" +
            string.Format("相同 {0} · 新增 {1} · 仅有 A {2} · 修改 {3}\r\n\r\n", same,added,removed,changed) + lines +
            "\r\n按大小写敏感的完整路径和解压后 SHA-256 比较；包含目录项。忽略时间戳、压缩方式、条目顺序及 ZIP 元数据。不会递归展开嵌套压缩包，也不会验证 JAR 签名。";
    }
    public static string Expected(string text, string algorithm) {
        string value = text.Trim(); int size = algorithm == "SHA256" ? 64 : algorithm == "SHA1" ? 40 : 32;
        if (value.Length != size || !System.Text.RegularExpressions.Regex.IsMatch(value,"\\A[0-9a-fA-F]+\\z"))
            throw new ArgumentException("请输入 " + size + " 位十六进制校验值（仅校验值，不含文件名）。");
        return value.ToLowerInvariant();
    }
}

public class MainForm : Form {
    TextBox a = new TextBox(), b = new TextBox(), expected = new TextBox(), output = new TextBox();
    ComboBox mode = new ComboBox(), algorithm = new ComboBox();
    Button run = new Button(), cancel = new Button(); Label state = new Label(); ProgressBar progress = new ProgressBar();
    CancellationTokenSource cancellation; TableLayoutPanel input;
    public MainForm() {
        Text = "Checksum Comparator"; Size = new Size(920,710); MinimumSize = new Size(720,590);
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft YaHei UI", 10); BackColor = Color.FromArgb(244,247,251);
        var root = new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(24), ColumnCount=1, RowCount=6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,75)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,190));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); Controls.Add(root);
        var title = new Label { Text="Checksum Comparator\n文件校验 · ZIP / JAR 内容比较 · 全程本地处理", Dock=DockStyle.Fill, Font=new Font(Font.FontFamily,14,FontStyle.Bold) }; root.Controls.Add(title);
        input = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=3, RowCount=4 };
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100)); input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,96));
        for(int i=0;i<4;i++) input.RowStyles.Add(new RowStyle(SizeType.Percent,25)); root.Controls.Add(input);
        mode.DropDownStyle=ComboBoxStyle.DropDownList; mode.Items.AddRange(new object[]{"比较两个文件", "验证已知校验值", "比较 ZIP / JAR 内部内容"}); mode.SelectedIndex=0;
        algorithm.DropDownStyle=ComboBoxStyle.DropDownList; algorithm.Items.AddRange(new object[]{"SHA256","SHA1","MD5"}); algorithm.SelectedIndex=0;
        AddRow(0,"比较方式",mode,algorithm); AddRow(1,"文件 A",a,Picker(a)); AddRow(2,"文件 B",b,Picker(b)); AddRow(3,"预期校验值",expected,new Label());
        a.AllowDrop=b.AllowDrop=true; BindDrop(a); BindDrop(b);
        mode.SelectedIndexChanged += delegate { b.Enabled=mode.SelectedIndex!=1; expected.Enabled=mode.SelectedIndex==1; algorithm.Enabled=mode.SelectedIndex!=2; };
        expected.Enabled=false;
        var buttons = new FlowLayoutPanel { Dock=DockStyle.Fill }; root.Controls.Add(buttons);
        run.Text="开始比较"; run.Width=120; run.Height=34; run.BackColor=Color.FromArgb(30,100,210); run.ForeColor=Color.White; run.FlatStyle=FlatStyle.Flat;
        cancel.Text="取消"; cancel.Enabled=false; cancel.Height=34;
        var copy = new Button {Text="复制结果",Width=110,Height=34};
        buttons.Controls.AddRange(new Control[]{run,cancel,copy}); run.Click += async delegate { await Execute(); };
        cancel.Click += delegate { if(cancellation!=null) cancellation.Cancel(); };
        copy.Click += delegate { if(output.TextLength>0) { try { Clipboard.SetText(output.Text); state.Text="结果已复制"; } catch(Exception ex) { state.Text=ex.Message; } } };
        state.Text="选择或拖入文件，然后开始比较。"; state.Dock=DockStyle.Fill; root.Controls.Add(state);
        output.Multiline=true; output.ReadOnly=true; output.ScrollBars=ScrollBars.Both; output.WordWrap=false; output.Dock=DockStyle.Fill; output.BackColor=Color.White; output.Font=new Font("Consolas",10); root.Controls.Add(output);
        progress.Dock=DockStyle.Fill; progress.Visible=false; progress.Style=ProgressBarStyle.Marquee; root.Controls.Add(progress);
        FormClosing += delegate { if(cancellation!=null) cancellation.Cancel(); };
    }
    void AddRow(int row,string label,Control center,Control end) {
        input.Controls.Add(new Label {Text=label,AutoSize=true,Anchor=AnchorStyles.Left},0,row); center.Dock=DockStyle.Fill; end.Dock=DockStyle.Fill; input.Controls.Add(center,1,row); input.Controls.Add(end,2,row);
    }
    Button Picker(TextBox target) { var button=new Button {Text="浏览…"}; button.Click += delegate { using(var d=new OpenFileDialog()) { d.Filter="所有文件|*.*"; if(d.ShowDialog()==DialogResult.OK) target.Text=d.FileName; } }; return button; }
    void BindDrop(TextBox target) {
        target.DragEnter += delegate(object sender,DragEventArgs e) { if(e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect=DragDropEffects.Copy; };
        target.DragDrop += delegate(object sender,DragEventArgs e) { var paths=(string[])e.Data.GetData(DataFormats.FileDrop); if(paths.Length>0) target.Text=paths[0]; if(target==a && paths.Length>1) b.Text=paths[1]; };
    }
    async Task Execute() {
        string pa=a.Text.Trim().Trim('"'), pb=b.Text.Trim().Trim('"'), exp=expected.Text, alg=(string)algorithm.SelectedItem; int selected=mode.SelectedIndex;
        cancellation=new CancellationTokenSource(); var token=cancellation.Token; input.Enabled=false; run.Enabled=false; cancel.Enabled=true; progress.Visible=true; state.Text="正在读取文件，请稍候…"; output.Clear();
        try {
            string report=await Task.Run(() => {
                if(selected==2) return "A: " + pa + "\r\nB: " + pb + "\r\n\r\n" + Engine.CompareArchives(pa,pb,token);
                string wanted=selected==1 ? Engine.Expected(exp,alg) : null;
                string ha=Engine.FileHash(pa,alg,token); string hb=selected==1 ? wanted : Engine.FileHash(pb,alg,token);
                return (ha==hb ? "校验和相同 ✓" : "校验和不同 ✗") + "\r\n\r\n算法: " + alg + "\r\nA: " + pa + "\r\n" + ha + "\r\n\r\n" + (selected==1 ? "预期校验值" : "B: " + pb) + "\r\n" + hb + "\r\n\r\n" + (alg=="SHA256" ? "比较文件内容；文件名不影响校验和。" : "MD5 / SHA-1 仅适合兼容旧校验值；安全验证请优先使用 SHA-256。") + "\r\n校验值应来自可信来源；相同不代表文件安全。";
            },token);
            if(!IsDisposed) { output.Text=report; state.Text="比较完成"; }
        } catch(OperationCanceledException) { if(!IsDisposed) state.Text="已取消"; }
        catch(Exception ex) { if(!IsDisposed) { state.Text="无法完成比较"; output.Text=ex.Message; } }
        finally { cancellation.Dispose(); cancellation=null; if(!IsDisposed) { input.Enabled=true; run.Enabled=true; cancel.Enabled=false; progress.Visible=false; } }
    }
    [STAThread] public static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
}
