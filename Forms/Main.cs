using System.Diagnostics;
using System.Drawing.Text;
using System.IO.Compression;
using System.Text;
using Avatar_Explorer.Classes;

namespace Avatar_Explorer.Forms
{
    public sealed partial class Main : Form
    {
        // Current Version
        private const string CurrentVersion = "v1.0.1";

        // Items Data
        public Item[] Items;

        // Current Path
        public CurrentPath CurrentPath = new();

        // Font
        private readonly PrivateFontCollection _fontCollection = new();
        public FontFamily GuiFont;

        // Search Mode
        private bool _authorMode;
        private bool _categoryMode;

        private readonly Image _copyImage;
        private readonly Image _trashImage;
        private readonly Image _editImage;

        private Window _openingWindow = Window.Nothing;

        public Main()
        {
            if (File.Exists("./Datas/CopyIcon.png") && File.Exists("./Datas/TrashIcon.png") &&
                File.Exists("./Datas/EditIcon.png"))
            {
                _copyImage = Image.FromFile("./Datas/CopyIcon.png");
                _trashImage = Image.FromFile("./Datas/TrashIcon.png");
                _editImage = Image.FromFile("./Datas/EditIcon.png");
            }
            else
            {
                MessageBox.Show("�A�C�R���t�@�C����������܂���ł����B�\�t�g��������x�_�E�����[�h���Ă��������B", "�G���[", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Items = Helper.LoadItemsData();
            AddFontFile();
            InitializeComponent();
            GenerateAvatarList();
            GenerateAuthorList();
            GenerateCategoryListLeft();

            Text = $"VRChat Avatar Explorer {CurrentVersion} by �Ղ����";
        }

        private void AddFontFile()
        {
            if (!File.Exists("./Datas/NotoSansJP-Regular.ttf"))
            {
                MessageBox.Show("�t�H���g�t�@�C����������܂���ł���", "�G���[", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _fontCollection.AddFontFile("./Datas/NotoSansJP-Regular.ttf");
            GuiFont = _fontCollection.Families[0];
        }

        // Generate List (LEFT)
        private void GenerateAvatarList()
        {
            AvatarPage.Controls.Clear();
            var index = 0;
            foreach (Item item in Items.Where(item => item.Type == ItemType.Avatar))
            {
                Button button = Helper.CreateButton(item.ImagePath, item.Title, "���: " + item.AuthorName, true,
                    item.Title);
                button.Location = new Point(0, (70 * index) + 7);
                button.Click += (_, _) =>
                {
                    CurrentPath.CurrentSelectedAvatar = item.Title;
                    CurrentPath.CurrentSelectedAuthor = null;
                    CurrentPath.CurrentSelectedCategory = ItemType.Unknown;
                    CurrentPath.CurrentSelectedItemCategory = null;
                    CurrentPath.CurrentSelectedItem = null;
                    _authorMode = false;
                    _categoryMode = false;
                    GenerateCategoryList();
                    PathTextBox.Text = GeneratePath();
                };
                AvatarPage.Controls.Add(button);
                index++;
            }
        }

        private void GenerateAuthorList()
        {
            AvatarAuthorPage.Controls.Clear();
            var index = 0;

            var authors = Array.Empty<Author>();
            foreach (Item item in Items)
            {
                if (authors.Any(author => author.AuthorName == item.AuthorName)) continue;
                authors = authors.Append(new Author
                {
                    AuthorName = item.AuthorName,
                    AuthorImagePath = item.AuthorImageFilePath
                }).ToArray();
            }

            foreach (var author in authors)
            {
                Button button = Helper.CreateButton(author.AuthorImagePath, author.AuthorName,
                    Items.Count(item => item.AuthorName == author.AuthorName) + "�̍���", true, author.AuthorName);
                button.Location = new Point(0, (70 * index) + 2);
                button.Click += (_, _) =>
                {
                    CurrentPath.CurrentSelectedAuthor = author;
                    CurrentPath.CurrentSelectedAvatar = null;
                    CurrentPath.CurrentSelectedCategory = ItemType.Unknown;
                    CurrentPath.CurrentSelectedItemCategory = null;
                    CurrentPath.CurrentSelectedItem = null;
                    _authorMode = true;
                    _categoryMode = false;
                    GenerateCategoryList();
                    PathTextBox.Text = GeneratePath();
                };
                AvatarAuthorPage.Controls.Add(button);
                index++;
            }
        }

        private void GenerateCategoryListLeft()
        {
            CategoryPage.Controls.Clear();
            var index = 0;
            foreach (ItemType itemType in Enum.GetValues(typeof(ItemType)))
            {
                if (itemType is ItemType.Unknown) continue;

                var items = Items.Where(item => item.Type == itemType);
                var itemCount = items.Count();
                if (itemCount == 0) continue;
                Button button = Helper.CreateButton("./Datas/FolderIcon.png", Helper.GetCategoryName(itemType),
                    itemCount + "�̍���", true);
                button.Location = new Point(0, (70 * index) + 2);
                button.Click += (_, _) =>
                {
                    CurrentPath.CurrentSelectedCategory = itemType;
                    _authorMode = false;
                    _categoryMode = true;
                    GenerateItems();
                    PathTextBox.Text = GeneratePath();
                };
                CategoryPage.Controls.Add(button);
                index++;
            }
        }

        // Generate List (RIGHT)
        private void GenerateCategoryList()
        {
            _openingWindow = Window.ItemCategoryList;
            ResetAvatarList();

            var index = 0;
            foreach (ItemType itemType in Enum.GetValues(typeof(ItemType)))
            {
                if (itemType is ItemType.Unknown) continue;
                var itemCount = _authorMode
                    ? Items.Count(item =>
                        item.Type == itemType && item.AuthorName == CurrentPath.CurrentSelectedAuthor?.AuthorName)
                    : Items.Count(item =>
                        item.Type == itemType && (item.SupportedAvatar.Contains(CurrentPath.CurrentSelectedAvatar) ||
                                                  item.SupportedAvatar.Length == 0));
                if (itemCount == 0) continue;
                Button button = Helper.CreateButton("./Datas/FolderIcon.png", Helper.GetCategoryName(itemType),
                    itemCount + "�̍���");
                button.Location = new Point(0, (70 * index) + 2);
                button.Click += (_, _) =>
                {
                    CurrentPath.CurrentSelectedCategory = itemType;
                    GenerateItems();
                    PathTextBox.Text = GeneratePath();
                };
                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        private void GenerateItems()
        {
            _openingWindow = Window.ItemList;
            ResetAvatarList();

            var filteredItems = Items.AsEnumerable();
            if (_authorMode)
            {
                filteredItems = Items.Where(item =>
                    item.Type == CurrentPath.CurrentSelectedCategory &&
                    item.AuthorName == CurrentPath.CurrentSelectedAuthor?.AuthorName);
            }
            else if (_categoryMode)
            {
                filteredItems = Items.Where(item =>
                    item.Type == CurrentPath.CurrentSelectedCategory);
            }
            else
            {
                filteredItems = Items.Where(item =>
                    item.Type == CurrentPath.CurrentSelectedCategory &&
                    (item.SupportedAvatar.Contains(CurrentPath.CurrentSelectedAvatar) ||
                     item.SupportedAvatar.Length == 0));
            }

            var index = 0;
            foreach (Item item in filteredItems)
            {
                Button button = Helper.CreateButton(item.ImagePath, item.Title, "���: " + item.AuthorName, false,
                    item.Title);
                button.Location = new Point(0, (70 * index) + 2);
                button.Click += (_, _) =>
                {
                    CurrentPath.CurrentSelectedItem = item;
                    GenerateItemCategoryList();
                    PathTextBox.Text = GeneratePath();
                };

                ContextMenuStrip contextMenuStrip = new();

                if (item.BoothId != -1)
                {
                    ToolStripMenuItem toolStripMenuItem = new("Booth�����N�̃R�s�[", _copyImage);
                    toolStripMenuItem.Click += (_, _) =>
                    {
                        try
                        {
                            Clipboard.SetText("https://booth.pm/ja/items/" + item.BoothId);
                        }
                        catch
                        {
                            MessageBox.Show("�N���b�v�{�[�h�ɃR�s�[�ł��܂���ł���", "�G���[", MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    ToolStripMenuItem toolStripMenuItem1 = new("Booth�����N���J��", _copyImage);
                    toolStripMenuItem1.Click += (_, _) =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://booth.pm/ja/items/" + item.BoothId,
                            UseShellExecute = true
                        });
                    };

                    contextMenuStrip.Items.Add(toolStripMenuItem);
                    contextMenuStrip.Items.Add(toolStripMenuItem1);
                }

                ToolStripMenuItem toolStripMenuItem2 = new("�폜", _trashImage);
                toolStripMenuItem2.Click += (_, _) =>
                {
                    Items = Items.Where(i => i.Title != item.Title).ToArray();
                    GenerateItems();
                };

                ToolStripMenuItem toolStripMenuItem3 = new("�ҏW", _editImage);
                toolStripMenuItem3.Click += (_, _) =>
                {
                    AddItem addItem = new(this, CurrentPath.CurrentSelectedCategory, true, item, null);
                    addItem.ShowDialog();
                    GenerateAvatarList();
                    GenerateAuthorList();
                    GenerateCategoryListLeft();
                };

                ToolStripMenuItem toolStripMenuItem4 = new("�T���l�C���ύX", _editImage);
                toolStripMenuItem4.Click += (_, _) =>
                {
                    OpenFileDialog ofd = new()
                    {
                        Filter = "�摜�t�@�C��|*.png;*.jpg",
                        Title = "�T���l�C���ύX",
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        MessageBox.Show("�T���l�C����ύX���܂����I\n�ύX�O: " + item.ImagePath + "\n�ύX��: " + ofd.FileName,
                            "����", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        item.ImagePath = ofd.FileName;
                        GenerateItems();
                    }
                };

                contextMenuStrip.Items.Add(toolStripMenuItem2);
                contextMenuStrip.Items.Add(toolStripMenuItem3);
                contextMenuStrip.Items.Add(toolStripMenuItem4);
                button.ContextMenuStrip = contextMenuStrip;
                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        private void GenerateItemCategoryList()
        {
            _openingWindow = Window.ItemFolderCategoryList;
            var types = new[]
            {
                "���ϗp�f�[�^",
                "�e�N�X�`��",
                "�h�L�������g",
                "Unity�p�b�P�[�W",
                "�s��"
            };
            if (CurrentPath.CurrentSelectedItem == null) return;
            ItemFolderInfo itemFolderInfo = Helper.GetItemFolderInfo(CurrentPath.CurrentSelectedItem.ItemPath);
            CurrentPath.CurrentSelectedItemFolderInfo = itemFolderInfo;

            ResetAvatarList();

            var index = 0;
            foreach (var itemType in types)
            {
                var itemCount = itemFolderInfo.GetItemCount(itemType);
                if (itemCount == 0) continue;
                Button button = Helper.CreateButton("./Datas/FolderIcon.png", itemType, itemCount + "�̍���");
                button.Location = new Point(0, (70 * index) + 2);
                button.Click += (_, _) =>
                {
                    CurrentPath.CurrentSelectedItemCategory = itemType;
                    GenerateItemFiles();
                    PathTextBox.Text = GeneratePath();
                };
                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        private void GenerateItemFiles()
        {
            _openingWindow = Window.ItemFolderItemsList;
            ResetAvatarList();

            var index = 0;
            foreach (var file in CurrentPath.CurrentSelectedItemFolderInfo.GetItems(CurrentPath
                         .CurrentSelectedItemCategory))
            {
                var imagePath = file.FileExtension is ".png" or ".jpg" ? file.FilePath : "./Datas/FileIcon.png";
                Button button = Helper.CreateButton(imagePath, file.FileName,
                    file.FileExtension.Replace(".", "") + "�t�@�C��", false, "�J���t�@�C���̃p�X: " + file.FilePath);
                button.Location = new Point(0, (70 * index) + 2);

                ContextMenuStrip contextMenuStrip = new();
                ToolStripMenuItem toolStripMenuItem = new("�t�@�C���̃p�X���J��", _copyImage);
                toolStripMenuItem.Click += (_, _) => { Process.Start("explorer.exe", "/select," + file.FilePath); };
                contextMenuStrip.Items.Add(toolStripMenuItem);
                button.ContextMenuStrip = contextMenuStrip;

                button.Click += (_, _) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = file.FilePath,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        Process.Start("explorer.exe", "/select," + file.FilePath);
                    }
                };
                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        private void GenerateFilteredItem(string[] searchWords)
        {
            ResetAvatarList();

            var filteredItems = Items
                .Where(item =>
                    searchWords.All(word =>
                        item.Title.ToLower().Contains(word.ToLower()) ||
                        item.AuthorName.ToLower().Contains(word.ToLower()) ||
                        item.SupportedAvatar.Any(avatar => avatar.ToLower().Contains(word.ToLower())) ||
                        item.BoothId.ToString().Contains(word.ToLower())
                    )
                )
                .OrderByDescending(item =>
                {
                    var matchCount = 0;
                    foreach (var word in searchWords)
                    {
                        if (item.Title.ToLower().Contains(word.ToLower())) matchCount++;
                        if (item.AuthorName.ToLower().Contains(word.ToLower())) matchCount++;
                    }

                    return matchCount;
                })
                .ToList();

            SearchResultLabel.Text = "��������: " + filteredItems.Count + "��" + " (�S" + Items.Length + "��)";

            var index = 0;
            foreach (Item item in filteredItems)
            {
                Button button = Helper.CreateButton(item.ImagePath, item.Title, "���: " + item.AuthorName, false,
                    item.Title);
                button.Location = new Point(0, (70 * index) + 2);
                button.Click += (_, _) =>
                {
                    _authorMode = false;
                    GeneratePathFromItem(item);
                    SearchBox.Text = "";
                    GenerateItemCategoryList();
                    PathTextBox.Text = GeneratePath();
                };

                ContextMenuStrip contextMenuStrip = new();

                if (item.BoothId != -1)
                {
                    ToolStripMenuItem toolStripMenuItem = new("Booth�����N�̃R�s�[", _copyImage);
                    toolStripMenuItem.Click += (_, _) =>
                    {
                        try
                        {
                            Clipboard.SetText("https://booth.pm/ja/items/" + item.BoothId);
                        }
                        catch
                        {
                            MessageBox.Show("�N���b�v�{�[�h�ɃR�s�[�ł��܂���ł���", "�G���[", MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    ToolStripMenuItem toolStripMenuItem1 = new("Booth�����N���J��", _copyImage);
                    toolStripMenuItem1.Click += (_, _) =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://booth.pm/ja/items/" + item.BoothId,
                            UseShellExecute = true
                        });
                    };

                    contextMenuStrip.Items.Add(toolStripMenuItem);
                    contextMenuStrip.Items.Add(toolStripMenuItem1);
                }

                ToolStripMenuItem toolStripMenuItem2 = new("�폜", _trashImage);
                toolStripMenuItem2.Click += (_, _) =>
                {
                    Items = Items.Where(i => i.Title != item.Title).ToArray();
                    GenerateItems();
                };

                ToolStripMenuItem toolStripMenuItem3 = new("�ҏW", _editImage);
                toolStripMenuItem3.Click += (_, _) =>
                {
                    AddItem addItem = new(this, item.Type, true, item, null);
                    addItem.ShowDialog();
                    GenerateAvatarList();
                    GenerateAuthorList();
                    GenerateCategoryListLeft();
                };

                contextMenuStrip.Items.Add(toolStripMenuItem2);
                contextMenuStrip.Items.Add(toolStripMenuItem3);
                button.ContextMenuStrip = contextMenuStrip;
                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        private void GenerateFilteredFolderItems(string[] searchWords)
        {
            ResetAvatarList();

            var fileDatas = _openingWindow switch
            {
                Window.ItemFolderItemsList => CurrentPath.CurrentSelectedItemFolderInfo.GetItems(CurrentPath
                    .CurrentSelectedItemCategory),
                Window.ItemFolderCategoryList => CurrentPath.CurrentSelectedItemFolderInfo.GetAllItem(),
                _ => Array.Empty<FileData>()
            };

            var filteredItems = fileDatas
                .Where(file =>
                    searchWords.All(word =>
                        file.FileName.ToLower().Contains(word.ToLower())
                    )
                )
                .OrderByDescending(file =>
                {
                    return searchWords.Count(word => file.FileName.ToLower().Contains(word.ToLower()));
                })
                .ToList();

            SearchResultLabel.Text = "�t�H���_�[����������: " + filteredItems.Count + "��" + " (�S" +
                                     fileDatas.Length + "��)";

            var index = 0;
            foreach (var file in filteredItems)
            {
                var imagePath = file.FileExtension is ".png" or ".jpg" ? file.FilePath : "./Datas/FileIcon.png";
                Button button = Helper.CreateButton(imagePath, file.FileName,
                    file.FileExtension.Replace(".", "") + "�t�@�C��", false, "�J���t�@�C���̃p�X: " + file.FilePath);
                button.Location = new Point(0, (70 * index) + 2);

                ContextMenuStrip contextMenuStrip = new();
                ToolStripMenuItem toolStripMenuItem = new("�t�@�C���̃p�X���J��", _copyImage);
                toolStripMenuItem.Click += (_, _) => { Process.Start("explorer.exe", "/select," + file.FilePath); };
                contextMenuStrip.Items.Add(toolStripMenuItem);
                button.ContextMenuStrip = contextMenuStrip;

                button.Click += (_, _) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = file.FilePath,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        Process.Start("explorer.exe", "/select," + file.FilePath);
                    }
                };
                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        // Add Item Form
        private void AddItemButton_Click(object sender, EventArgs e)
        {
            AddItem addItem = new AddItem(this, CurrentPath.CurrentSelectedCategory, false, null, null);
            addItem.ShowDialog();
            GenerateAvatarList();
            GenerateAuthorList();
            GenerateCategoryListLeft();
        }

        // Generate Path
        private string GeneratePath()
        {
            if (_authorMode)
            {
                if (CurrentPath.CurrentSelectedAuthor == null) return "";
                if (CurrentPath.CurrentSelectedCategory == ItemType.Unknown)
                    return Helper.RemoveFormat(CurrentPath.CurrentSelectedAuthor.AuthorName);
                if (CurrentPath.CurrentSelectedItem == null)
                    return Helper.RemoveFormat(CurrentPath.CurrentSelectedAuthor.AuthorName) + "/" +
                           Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory);
                if (CurrentPath.CurrentSelectedItemCategory == null)
                    return Helper.RemoveFormat(CurrentPath.CurrentSelectedAuthor.AuthorName) + "/" +
                           Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory) + "/" +
                           Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title);

                return Helper.RemoveFormat(CurrentPath.CurrentSelectedAuthor.AuthorName) + "/" +
                       Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory) + "/" +
                       Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title) + "/" +
                       CurrentPath.CurrentSelectedItemCategory;
            }

            if (_categoryMode)
            {
                if (CurrentPath.CurrentSelectedCategory == ItemType.Unknown) return "";
                if (CurrentPath.CurrentSelectedItem == null)
                    return Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory);
                if (CurrentPath.CurrentSelectedItemCategory == null)
                    return Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory) + "/" +
                           Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title);

                return Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory) + "/" +
                       Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title) + "/" +
                       CurrentPath.CurrentSelectedItemCategory;
            }

            if (CurrentPath.CurrentSelectedAvatar == null) return "";
            if (CurrentPath.CurrentSelectedCategory == ItemType.Unknown)
                return Helper.RemoveFormat(CurrentPath.CurrentSelectedAvatar);
            if (CurrentPath.CurrentSelectedItem == null)
                return Helper.RemoveFormat(CurrentPath.CurrentSelectedAvatar) + "/" +
                       Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory);
            if (CurrentPath.CurrentSelectedItemCategory == null)
                return Helper.RemoveFormat(CurrentPath.CurrentSelectedAvatar) + "/" +
                       Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory) + "/" +
                       Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title);

            return Helper.RemoveFormat(CurrentPath.CurrentSelectedAvatar) + "/" +
                   Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory) + "/" +
                   Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title) + "/" +
                   Helper.RemoveFormat(CurrentPath.CurrentSelectedItemCategory);
        }

        private void GeneratePathFromItem(Item item)
        {
            CurrentPath.CurrentSelectedAvatar = item.SupportedAvatar.FirstOrDefault() ?? "*";
            CurrentPath.CurrentSelectedCategory = item.Type;
            CurrentPath.CurrentSelectedItem = item;
        }

        // Undo Button
        private void UndoButton_Click(object sender, EventArgs e)
        {
            if (CurrentPath.CurrentSelectedItemCategory != null)
            {
                CurrentPath.CurrentSelectedItemCategory = null;
                GenerateItemCategoryList();
                PathTextBox.Text = GeneratePath();
                return;
            }

            if (CurrentPath.CurrentSelectedItem != null)
            {
                CurrentPath.CurrentSelectedItem = null;
                GenerateItems();
                PathTextBox.Text = GeneratePath();
                return;
            }

            if (_categoryMode) return;

            if (CurrentPath.CurrentSelectedCategory != ItemType.Unknown)
            {
                CurrentPath.CurrentSelectedCategory = ItemType.Unknown;
                GenerateCategoryList();
                PathTextBox.Text = GeneratePath();
            }

            if (CurrentPath.CurrentSelectedAvatar == "*")
            {
                CurrentPath.CurrentSelectedAvatar = null;
                ResetAvatarList(true);
                PathTextBox.Text = GeneratePath();
            }
        }

        // Save Config
        private void Main_FormClosing(object sender, FormClosingEventArgs e) => Helper.SaveItemsData(Items);

        // Search Box
        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            if (SearchBox.Text == "")
            {
                SearchResultLabel.Text = "";
                if (CurrentPath.CurrentSelectedItemCategory != null)
                {
                    GenerateItemFiles();
                    return;
                }

                if (CurrentPath.CurrentSelectedItem != null)
                {
                    GenerateItemCategoryList();
                    return;
                }

                if (CurrentPath.CurrentSelectedCategory != ItemType.Unknown)
                {
                    GenerateItems();
                    return;
                }

                if (CurrentPath.CurrentSelectedAvatar != null || CurrentPath.CurrentSelectedAuthor != null)
                {
                    GenerateCategoryList();
                    return;
                }

                ResetAvatarList(true);
                return;
            }

            string[] searchWords = SearchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (_openingWindow is Window.ItemFolderCategoryList or Window.ItemFolderItemsList)
            {
                GenerateFilteredFolderItems(searchWords);
            }
            else
            {
                GenerateFilteredItem(searchWords);
            }
        }

        // ResetAvatarList
        private void ResetAvatarList(bool startLabelVisible = false)
        {
            for (int i = AvatarItemExplorer.Controls.Count - 1; i >= 0; i--)
            {
                if (AvatarItemExplorer.Controls[i].Name != "StartLabel")
                {
                    AvatarItemExplorer.Controls.RemoveAt(i);
                }
                else
                {
                    AvatarItemExplorer.Controls[i].Visible = startLabelVisible;
                }
            }
        }

        // Drag and Drop Item Folder
        private void AvatarItemExplorer_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[]? dragFilePathArr = (string[]?)e.Data.GetData(DataFormats.FileDrop, false);
            if (dragFilePathArr == null) return;
            var folderPath = dragFilePathArr[0];

            if (File.Exists(folderPath))
            {
                MessageBox.Show("�t�H���_��I�����Ă�������", "�G���[", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AddItem addItem = new(this, CurrentPath.CurrentSelectedCategory, false, null, folderPath);
            addItem.ShowDialog();
        }

        private void AvatarPage_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[]? dragFilePathArr = (string[]?)e.Data.GetData(DataFormats.FileDrop, false);
            if (dragFilePathArr == null) return;
            var folderPath = dragFilePathArr[0];

            if (File.Exists(folderPath))
            {
                MessageBox.Show("�t�H���_��I�����Ă�������", "�G���[", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AddItem addItem = new(this, ItemType.Avatar, false, null, folderPath);
            addItem.ShowDialog();
            GenerateAvatarList();
            GenerateAuthorList();
            GenerateCategoryListLeft();
        }

        // Export to CSV
        private void ExportButton_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists("./Output"))
            {
                Directory.CreateDirectory("./Output");
            }

            var fileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv";

            using var sw = new StreamWriter("./Output/" + fileName, false, Encoding.UTF8);
            sw.WriteLine("Title,AuthorName,AuthorImageFilePath,ImagePath,Type,SupportedAvatar,BoothId,ItemPath");
            foreach (var item in Items)
            {
                sw.WriteLine(
                    $"{item.Title},{item.AuthorName},{item.AuthorImageFilePath},{item.ImagePath},{item.Type},{string.Join(";", item.SupportedAvatar)},{item.BoothId},{item.ItemPath}");
            }

            MessageBox.Show("Output�t�H���_�ɃG�N�X�|�[�g���������܂����I\n�t�@�C����: " + fileName, "����", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // Make Backup
        private void MakeBackupButton_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists("./Backup"))
            {
                Directory.CreateDirectory("./Backup");
            }

            var fileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".zip";

            try
            {
                ZipFile.CreateFromDirectory("./Datas", "./Backup/" + fileName);
            }
            catch
            {
                MessageBox.Show("�o�b�N�A�b�v�Ɏ��s���܂���", "�G���[", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("Backup�t�H���_�Ƀo�b�N�A�b�v���������܂����I\n\n�����������ꍇ��Backup�t�H���_�̒��g���𓀂��A�S�Ă�Datas�t�H���_�̒��g�ƒu��������Α��v�ł��I\n���\�t�g�͂��̊ԋN�����Ȃ��悤�ɂ��Ă��������I\n\n�t�@�C����: " + fileName, "����", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}