using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace DynamicFontGenerator
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private Dictionary<string, string> Dynamicfonts = new Dictionary<string, string>();
        private void Form1_Load(object sender, EventArgs e)
        {
            listView1.Items.Cast<ListViewItem>().ToList().ForEach(item =>
            {
                item.SubItems[1].Name = "Dynamicfont";
                item.SubItems[2].Name = "FontName";
                item.SubItems[3].Name = "Size";
            });

            string sampleFontPath = Path.Combine(Application.StartupPath, "SampleFont.dynamicfont");
            if (!File.Exists(sampleFontPath))
            {
                File.WriteAllText(sampleFontPath, Resource1.SampleFont_dynamicfont, Encoding.UTF8);
            }

            Directory.GetFiles(Application.StartupPath, "*.dynamicfont").ToList().ForEach(path =>
            {
                string name = Path.GetFileName(path).Replace(".dynamicfont", "");
                Dynamicfonts.Add(name, File.ReadAllText(path));
            });
            comboBox1.Items.AddRange(Dynamicfonts.Select(x => x.Key).ToArray());
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    textBox1.Text = dlg.SelectedPath;
                }
            }
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            var info = listView1.HitTest(e.X, e.Y);
            switch (info.SubItem.Name)
            {
                case "Dynamicfont":
                    comboBox1.Location = new Point(
                        listView1.Location.X + listView1.GetItemRect(info.Item.Index).Location.X + listView1.Columns[0].Width,
                        listView1.Location.Y + listView1.GetItemRect(info.Item.Index).Location.Y);
                    comboBox1.Width = listView1.Columns[1].Width;
                    comboBox1.Visible = true;
                    break;
                case "FontName":
                case "Size":
                    using (FontDialog dlg = new FontDialog())
                    {
                        dlg.Font = (Font)info.Item.SubItems[2].Tag ?? new Font(info.Item.SubItems[2].Text, float.Parse(info.Item.SubItems[3].Text));
                        if (dlg.ShowDialog() == DialogResult.OK)
                        {
                            info.Item.SubItems[2].Tag = dlg.Font;
                            info.Item.SubItems[2].Text = dlg.Font.Name;
                            info.Item.SubItems[3].Text = Math.Round(dlg.Font.Size).ToString();
                        }
                    }
                    break;
            }
        }

        private void comboBox1_MouseLeave(object sender, EventArgs e)
        {
            comboBox1.Visible = false;
        }
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            listView1.SelectedItems[0].SubItems[1].Text = comboBox1.Text;
            comboBox1.Visible = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            using (FontDialog dlg = new FontDialog())
            {
                dlg.Font = (Font)button3.Tag;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    listView1.Items.Cast<ListViewItem>().ToList().ForEach(item =>
                    {
                        item.SubItems[2].Tag = dlg.Font;
                        item.SubItems[2].Text = dlg.Font.Name;
                        item.SubItems[3].Text = Math.Round(dlg.Font.Size).ToString();
                    });
                    button3.Tag = dlg.Font;
                }
            }
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            textBox2.Text = string.Empty;
            textBox2.Dock = DockStyle.Fill;
            textBox2.Visible = true;
            try
            {
                Dictionary<KeyValuePair<string, string>, GeneratInfo> dic = new Dictionary<KeyValuePair<string, string>, GeneratInfo>();
                foreach (ListViewItem item in listView1.Items)
                {
                    string target = item.SubItems[0].Text;
                    string fontName = item.SubItems[2].Text;
                    string fontSize = item.SubItems[3].Text;

                    KeyValuePair<string, string> font = new KeyValuePair<string, string>(fontName, fontSize);
                    if (!dic.ContainsKey(font))
                    {
                        GeneratInfo info = new GeneratInfo();
                        info.Name = Dynamicfonts[item.SubItems[1].Text];
                        info.Text = Dynamicfonts[item.SubItems[1].Text];
                        info.Text = info.Text.Replace("{FontName}", fontName);
                        info.Text = info.Text.Replace("{FontSize}", fontSize);
                        info.DescFilePath = Path.Combine(Application.StartupPath, $"{fontName}{fontSize}");
                        info.CopyPaths = new List<string>();
                        dic.Add(font, info);
                    }
                    dic[font].CopyPaths.Add(Path.Combine(textBox1.Text, "Content", "Fonts", $"{target}.xnb"));
                }

                string logPath = Path.Combine(Application.StartupPath, "log.txt");
                using (var stdout = new StreamWriter(File.Open(logPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite)))
                using (var reader = new StreamReader(File.Open(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)))
                {
                    stdout.AutoFlush = true;
                    Console.SetOut(stdout);
                    try
                    {
                        dic.ToList().ForEach(x =>
                        {
                            File.WriteAllText(x.Value.DescFilePath, x.Value.Text, Encoding.UTF8);
                        });
                        await Task.Run(() =>
                        {
                            using (var game = new Generator(dic.Select(x => x.Value).ToList()))
                            {
                                bool b = true;
                                Task.Run(() =>
                                {
                                    while (b)
                                    {
                                        textBox2.Invoke(new Action(() => textBox2.Text += reader.ReadToEnd()));
                                        Thread.Sleep(100);
                                    }
                                });
                                game.Run();
                                b = false;
                            }
                        });
                        textBox2.Text += Environment.NewLine + Environment.NewLine;
                        dic.ToList().ForEach(x =>
                        {
                            x.Value.CopyPaths.ForEach(y => {
                                string path = $"{x.Value.DescFilePath}.xnb";
                                File.Copy(path, y, true);
                                textBox2.Text += $"copy {path} to {y}{Environment.NewLine}";
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        MessageBox.Show(ex.Message);
                    }
                }
                dic.ToList().ForEach(x => File.Delete(x.Value.DescFilePath));
                File.Delete(logPath);
            }
            catch (Exception ex)
            {
                textBox2.Text += ex.Message;
                MessageBox.Show(ex.Message);
            }
            MessageBox.Show("Generation is over.");
            textBox2.Visible = false;
        }
    }
}
