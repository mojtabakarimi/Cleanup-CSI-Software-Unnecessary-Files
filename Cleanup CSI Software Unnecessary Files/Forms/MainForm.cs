using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace CSICleanup.Forms
{
    public partial class MainForm : Form
    {
        private List<string> potentialFiles;

        private readonly string[] sapExtensions = { "ico", "msh", "tlog", "OUT", "sbk", "K~0", "K~I", "K~J", "K~M", "tlog", "Y", "Y$$", "Y~", "Y~1", "Y00", "Y01", "Y02", "Y03", "Y04", "Y05", "Y06", "Y07", "Y08", "C3", "F3", "K", "K3", "M3", "K~E", "K~G", "tlog" };
        private readonly string[] safeExtensions = { "ico", "K~0", "K~I", "K~J", "K~L", "K~M", "LOG", "msh", "OUT", "xsdm", "Y", "Y$$", "Y~", "Y~1", "Y00", "Y0A", "Y0B", "Y01", "Y02", "Y03", "Y05", "Y06", "Y07", "Y08", "Y09" };
        private readonly string[] etabsExtensions = {"ebk", "ico", "K~0", "K~E", "K~G", "K~I", "K~J", "K~M", "LOG", "msh", "OUT", "Y", "Y$$", "Y~", "Y~1", "Y00", "Y0A", "Y01", "Y02", "Y03", "Y04", "Y05", "Y06", "Y07", "Y08", "Y09"};

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            //var tt = Clipboard.GetFileDropList();

            //var ttt = string.Join("\r\n", tt.Cast<object>().ToArray());
            //Clipboard.SetText(ttt);
            
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = $@"{Application.ProductName} - v.{version.Major}.{version.Minor}.{version.Build}";
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            var path = PathTextBox.Text;
            var folderBrowserDialog = new CommonOpenFileDialog
            {
                InitialDirectory = path,
                IsFolderPicker = true
            };

            if (folderBrowserDialog.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }

            this.PathTextBox.Text = folderBrowserDialog.FileName;
            this.CheckButton.Enabled = true;
            pathListBox.Items.Clear();
        }

        private void CheckButton_Click(object sender, EventArgs e)
        {
            pathListBox.Items.Clear();

            var softwares = new List<string>();
            var extensions = new List<string>();
            if (chkSAP2000.Checked)
            {
                softwares.Add(".sdb");
                extensions.AddRange(sapExtensions);
            }
            if (chkETABS.Checked)
            {
                softwares.Add(".edb");
                extensions.AddRange(etabsExtensions);
            }
            if (chkSAFE.Checked)
            {
                softwares.Add(".fdb");
                extensions.AddRange(safeExtensions);
            }

            if (softwares.Count == 0)
            {
                MessageBox.Show("Please select at least one of the softwares", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            extensions = extensions.Distinct().ToList();

            var path = PathTextBox.Text;
            if (!path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
            {
                path += System.IO.Path.DirectorySeparatorChar.ToString();
            }

            var searchedFiles = SearchAllFiles(path, softwares.ToArray());

            potentialFiles = new List<string>();
            foreach (var file in searchedFiles)
            {
                var pureName = System.IO.Path.GetDirectoryName(file);
                if (string.IsNullOrEmpty(pureName))
                {
                    continue;
                }
                if (!pureName.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                {
                    pureName += System.IO.Path.DirectorySeparatorChar.ToString();
                }
                pureName += System.IO.Path.GetFileNameWithoutExtension(file);

                foreach (var ext in extensions)
                {
                    var fileName = $"{pureName}.{ext}";
                    if (System.IO.File.Exists(fileName))
                    {
                        potentialFiles.Add(fileName);
                    }
                }
            }

            var totalSize = potentialFiles.Sum(t => new System.IO.FileInfo(t).Length);
            pathListBox.Items.AddRange(potentialFiles.Cast<object>().ToArray());

            MessageBox.Show($@"{potentialFiles.Count} files (total size : {totalSize / 1024.0 / 1024.0:F2} MB) found", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);

            DeleteAllButton.Enabled = totalSize > 0;
        }

        private void DeleteAllButton_Click(object sender, EventArgs e)
        {
            var counter = 0;
            if (potentialFiles == null)
            {
                return;
            }

            if (MessageBox.Show("Are you sure you want to delete these files?\n There isn't any undo available!", Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            double totalLength = 0;
            foreach (var file in potentialFiles)
            {
                try
                {
                    var length = new System.IO.FileInfo(file).Length;
                    totalLength += length;
                    System.IO.File.Delete(file);
                }
                catch
                {
                    counter++;
                }
            }

            pathListBox.Items.Clear();
            DeleteAllButton.Enabled = false;

            MessageBox.Show($@"{potentialFiles.Count - counter} files from {potentialFiles.Count}, ({totalLength / 1024 / 1024:F2} MB) has been deleted!", Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private IEnumerable<string> SearchAllFiles(string path, string extension, bool includeSubDir = true)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return null;
            }
            if (!extension.StartsWith("."))
            {
                extension = extension.Insert(0, ".");
            }

            extension = extension.ToLower();

            var files = System.IO.Directory.GetFiles(path);
            var fileCollection = files.Where(file => System.IO.Path.GetExtension(file).ToLower() == extension).ToList();

            if (!includeSubDir)
            {
                return fileCollection.ToArray();
            }

            var directories = System.IO.Directory.GetDirectories(path);
            foreach (var directory in directories)
            {
                fileCollection.AddRange(SearchAllFiles(directory, extension));
            }

            return fileCollection.ToArray();
        }

        private IEnumerable<string> SearchAllFiles(string path, string[] extension, bool includeSubDir = true)
        {
            if (extension == null || extension.Length == 0)
            {
                return null;
            }

            extension = extension
                .Select(p => p.StartsWith(".") ? p : $".{p}")
                .Select(p => p.ToLower())
                .ToArray();

            var files = System.IO.Directory.GetFiles(path);
            var fileCollection = files
                .Where(file => extension.Contains(System.IO.Path.GetExtension(file).ToLower()))
                .ToList();

            if (!includeSubDir)
            {
                return fileCollection.ToArray();
            }

            var directories = System.IO.Directory.GetDirectories(path);
            foreach (var directory in directories)
            {
                fileCollection.AddRange(SearchAllFiles(directory, extension));
            }

            return fileCollection.ToArray();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void toolStripStatusLabel1_Click_1(object sender, EventArgs e)
        {
            Process.Start("https://www.linkedin.com/in/karimimojtaba/");
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
