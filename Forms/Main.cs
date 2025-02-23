using System.Diagnostics;
using System.Drawing.Text;
using System.IO.Compression;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using Avatar_Explorer.Classes;
using Timer = System.Windows.Forms.Timer;

namespace Avatar_Explorer.Forms
{
    public sealed partial class Main : Form
    {
        // Current Version
        private const string CurrentVersion = "v1.0.4";

        // Current Version Form Text
        private const string CurrentVersionFormText = $"VRChat Avatar Explorer {CurrentVersion} by �Ղ����";

        // Min Resize Font Size
        private const float MinFontSize = 8f;

        // Backup Interval
        private const int BackupInterval = 300000; // 5 Minutes

        // Items Data
        public Item[] Items;

        // Common Avatars
        public CommonAvatar[] CommonAvatars;

        // Custom Categories
        public string[] CustomCategories;

        // Current Path
        public CurrentPath CurrentPath = new();

        // Font
        private readonly PrivateFontCollection _fontCollection = new();
        private readonly Dictionary<string, FontFamily> _fontFamilies = new();
        public FontFamily? GuiFont;

        // Language
        public string CurrentLanguage = "ja-JP";

        // Search Mode
        private bool _authorMode;
        private bool _categoryMode;

        private Window _openingWindow = Window.Nothing;

        // Dictionary For Resize Controls
        private readonly Dictionary<string, string> _controlNames = new();
        private readonly Dictionary<string, SizeF> _defaultControlSize = new();
        private readonly Dictionary<string, PointF> _defaultControlLocation = new();
        private readonly Dictionary<string, float> _defaultFontSize = new();

        // Initial Form, AvatarSearchFilterList, AvatarItemExplorer Size
        private readonly Size _initialFormSize;
        private readonly int _baseAvatarSearchFilterListWidth;
        private readonly int _baseAvatarItemExplorerListWidth;

        /// <summary>
        /// Get AvatarList Width
        /// </summary>
        /// <returns>AvatarList Width</returns>
        private int GetAvatarListWidth() => AvatarSearchFilterList.Width - _baseAvatarSearchFilterListWidth;

        /// <summary>
        /// Get ItemExplorerList Width
        /// </summary>
        /// <returns>ItemExplorerList Width</returns>
        private int GetItemExplorerListWidth() => AvatarItemExplorer.Width - _baseAvatarItemExplorerListWidth;

        // Last Backup Time
        private DateTime _lastBackupTime;

        // Last Backup Error
        private bool _lastBackupError;

        // Searching Bool
        private bool _isSearching;

        // Initialized Bool
        private readonly bool _initialized;

        public Main()
        {
            try
            {
                Items = Helper.LoadItemsData();
                CommonAvatars = Helper.LoadCommonAvatarData();

                // Fix Supported Avatar Path (Title => Path)
                Items = Helper.FixSupportedAvatarPath(Items);

                AddFontFile();
                CustomCategories = Helper.LoadCustomCategoriesData();
                InitializeComponent();

                // Save the default Size
                _initialFormSize = ClientSize;
                _baseAvatarSearchFilterListWidth = AvatarSearchFilterList.Width;
                _baseAvatarItemExplorerListWidth = AvatarItemExplorer.Width;
                _initialized = true;

                // Render Window
                RefleshWindow();

                // Start AutoBackup
                AutoBackup();

                // Set Backup Title Loop
                BackupTimeTitle();

                Text = $"VRChat Avatar Explorer {CurrentVersion} by �Ղ����";
            }
            catch (Exception ex)
            {
                MessageBox.Show("�\�t�g�̋N�����ɃG���[���������܂����B\n\n" + ex,
                    "�G���[", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }
        }

        private void AddFontFile()
        {
            string[] fontFiles = Directory.GetFiles("./Datas/Fonts", "*.ttf");
            foreach (var fontFile in fontFiles)
            {
                _fontCollection.AddFontFile(fontFile);
            }

            foreach (var fontFamily in _fontCollection.Families)
            {
                switch (fontFamily.Name)
                {
                    case "Noto Sans JP":
                        _fontFamilies.Add("ja-JP", fontFamily);
                        break;
                    case "Noto Sans":
                        _fontFamilies.Add("en-US", fontFamily);
                        break;
                    case "Noto Sans KR":
                        _fontFamilies.Add("ko-KR", fontFamily);
                        break;
                }
            }

            var newFont = _fontFamilies.TryGetValue(CurrentLanguage, out var family) ? family : _fontFamilies["ja-JP"];
            GuiFont = newFont;
        }

        // Generate List (LEFT)
        private void GenerateAvatarList()
        {
            ResetAvatarPage(AvatarPage);

            var items = Items.Where(item => item.Type == ItemType.Avatar).ToArray();
            if (items.Length == 0) return;

            items = items.OrderBy(item =>
            {
                var mode = SortingBox.SelectedIndex;
                return mode switch
                {
                    0 => item.Title,
                    1 => item.AuthorName,
                    _ => item.Title
                };
            }).ToArray();

            var index = 0;
            foreach (Item item in items)
            {
                var description = item.Title;
                if (!string.IsNullOrEmpty(item.ItemMemo))
                {
                    description += "\n\n" + Helper.Translate("����: ", CurrentLanguage) + item.ItemMemo;
                }

                Button button = Helper.CreateButton(item.ImagePath, item.Title,
                    Helper.Translate("���: ", CurrentLanguage) + item.AuthorName, true,
                    description, GetAvatarListWidth());
                button.Location = new Point(0, (70 * index) + 2);

                EventHandler clickEvent = (_, _) =>
                {
                    CurrentPath = new CurrentPath
                    {
                        CurrentSelectedAvatar = item.Title,
                        CurrentSelectedAvatarPath = item.ItemPath
                    };
                    _authorMode = false;
                    _categoryMode = false;
                    SearchBox.Text = "";
                    SearchResultLabel.Text = "";
                    _isSearching = false;
                    GenerateCategoryList();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) =>
                {
                    button.Click -= clickEvent;
                    button.ContextMenuStrip?.Dispose();
                };

                ContextMenuStrip contextMenuStrip = new();

                if (item.BoothId != -1)
                {
                    ToolStripMenuItem toolStripMenuItem =
                        new(Helper.Translate("Booth�����N�̃R�s�[", CurrentLanguage),
                            SharedImages.GetImage(SharedImages.Images.CopyIcon));
                    EventHandler clickEvent2 = (_, _) =>
                    {
                        try
                        {
                            Clipboard.SetText(
                                $"https://booth.pm/{Helper.GetCurrentLanguageCode(CurrentLanguage)}/items/" +
                                item.BoothId);
                        }
                        catch (Exception ex)
                        {
                            if (ex is ExternalException) return;
                            MessageBox.Show(Helper.Translate("�N���b�v�{�[�h�ɃR�s�[�ł��܂���ł���", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    toolStripMenuItem.Click += clickEvent2;
                    toolStripMenuItem.Disposed += (_, _) => toolStripMenuItem.Click -= clickEvent2;

                    ToolStripMenuItem toolStripMenuItem1 =
                        new(Helper.Translate("Booth�����N���J��", CurrentLanguage),
                            SharedImages.GetImage(SharedImages.Images.CopyIcon));
                    EventHandler clickEvent3 = (_, _) =>
                    {
                        try
                        {
                            Process.Start($"https://booth.pm/{Helper.GetCurrentLanguageCode(CurrentLanguage)}/items/" +
                                          item.BoothId);
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�����N���J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    toolStripMenuItem1.Click += clickEvent3;
                    toolStripMenuItem1.Disposed += (_, _) => toolStripMenuItem1.Click -= clickEvent3;

                    contextMenuStrip.Items.Add(toolStripMenuItem);
                    contextMenuStrip.Items.Add(toolStripMenuItem1);
                }

                ToolStripMenuItem toolStripMenuItem2 = new(Helper.Translate("���̍�҂̑��̃A�C�e����\��", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.OpenIcon));
                EventHandler? clickEvent4 = (_, _) =>
                {
                    SearchBox.Text = $"Author=\"{item.AuthorName}\"";
                    SearchItems();
                };

                toolStripMenuItem2.Click += clickEvent4;
                toolStripMenuItem2.Disposed += (_, _) => toolStripMenuItem2.Click -= clickEvent4;

                ToolStripMenuItem toolStripMenuItem3 = new(Helper.Translate("�T���l�C���ύX", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent5 = (_, _) =>
                {
                    OpenFileDialog ofd = new()
                    {
                        Filter = Helper.Translate("�摜�t�@�C��|*.png;*.jpg", CurrentLanguage),
                        Title = Helper.Translate("�T���l�C���ύX", CurrentLanguage),
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() != DialogResult.OK) return;
                    MessageBox.Show(
                        Helper.Translate("�T���l�C����ύX���܂����I", CurrentLanguage) + "\n\n" +
                        Helper.Translate("�ύX�O: ", CurrentLanguage) + item.ImagePath + "\n\n" +
                        Helper.Translate("�ύX��: ", CurrentLanguage) + ofd.FileName,
                        Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    item.ImagePath = ofd.FileName;

                    // �����A�o�^�[�̗����E�ŊJ���Ă�����A���̃T���l�C�����X�V���Ȃ��Ƃ����Ȃ����߁B
                    if (_openingWindow == Window.ItemList && !_isSearching) GenerateItems();

                    //���������ƁA������ʂ��ēǍ����Ă�����
                    if (_isSearching) SearchItems();

                    GenerateAvatarList();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem3.Click += clickEvent5;
                toolStripMenuItem3.Disposed += (_, _) => toolStripMenuItem3.Click -= clickEvent5;

                ToolStripMenuItem toolStripMenuItem4 = new(Helper.Translate("�ҏW", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent6 = (_, _) =>
                {
                    var prePath = item.ItemPath;

                    AddItem addItem = new(this, item.Type, item.CustomCategory, true, item, null);
                    addItem.ShowDialog();

                    //�Ή��A�o�^�[�̃p�X��ς��Ă�����
                    foreach (var item2 in Items)
                    {
                        if (item2.SupportedAvatar.Contains(prePath))
                        {
                            item2.SupportedAvatar = item2.SupportedAvatar.Select(avatar =>
                                avatar == prePath ? item.ItemPath : avatar).ToArray();
                        }
                    }

                    // �����A�C�e���ŕҏW���ꂽ�A�C�e�����J���Ă�����A�p�X�ȂǂɎg�p����镶������X�V���Ȃ��Ƃ����Ȃ�����
                    if (CurrentPath.CurrentSelectedAvatarPath == prePath)
                    {
                        CurrentPath.CurrentSelectedAvatar = item.Title;
                        CurrentPath.CurrentSelectedAvatarPath = item.ItemPath;
                    }

                    // �����A�o�^�[�̗����E�ŊJ���Ă�����A���̃A�C�e���̏����X�V���Ȃ��Ƃ����Ȃ�����
                    if (_openingWindow == Window.ItemList && !_isSearching) GenerateItems();

                    //���������ƁA������ʂ��ēǍ����Ă�����
                    if (_isSearching) SearchItems();

                    // �������̕�����������Ȃ��悤�ɂ��邽�߂�_isSearching�Ń`�F�b�N���Ă���
                    if (!_isSearching) PathTextBox.Text = GeneratePath();

                    RefleshWindow();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem4.Click += clickEvent6;
                toolStripMenuItem4.Disposed += (_, _) => toolStripMenuItem4.Click -= clickEvent6;

                ToolStripMenuItem toolStripMenuItem5 = new(Helper.Translate("�����̒ǉ�", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent7 = (_, _) =>
                {
                    var previouseMemo = item.ItemMemo;
                    AddNote addMemo = new(this, item);
                    addMemo.ShowDialog();

                    var memo = addMemo.Memo;
                    if (string.IsNullOrEmpty(memo) || memo == previouseMemo) return;

                    item.ItemMemo = memo;

                    RefleshWindow();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem5.Click += clickEvent7;
                toolStripMenuItem5.Disposed += (_, _) => toolStripMenuItem5.Click -= clickEvent7;

                ToolStripMenuItem toolStripMenuItem6 = new(Helper.Translate("�폜", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.TrashIcon));
                EventHandler clickEvent8 = (_, _) =>
                {
                    DialogResult result = MessageBox.Show(Helper.Translate("�{���ɍ폜���܂����H", CurrentLanguage),
                        Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) return;

                    var undo = false; //�����폜�����A�C�e�����J����Ă�����
                    if (CurrentPath.CurrentSelectedItem?.ItemPath == item.ItemPath)
                    {
                        CurrentPath.CurrentSelectedItemCategory = null;
                        CurrentPath.CurrentSelectedItem = null;
                        undo = true;
                    }

                    var undo2 = false; //�A�o�^�[���[�h�ł����폜�����A�o�^�[���獡�܂ł̃A�C�e�����J����Ă�����
                    if (CurrentPath.CurrentSelectedAvatarPath == item.ItemPath && !_authorMode && !_categoryMode)
                    {
                        CurrentPath = new CurrentPath();
                        undo2 = true;
                    }

                    Items = Items.Where(i => i.ItemPath != item.ItemPath).ToArray();

                    // �A�o�^�[�̂Ƃ��͑Ή��A�o�^�[�폜�A���ʑf�̃O���[�v����폜�p�̏��������s����
                    if (item.Type == ItemType.Avatar)
                    {
                        var result2 = MessageBox.Show(
                            Helper.Translate("���̃A�o�^�[��Ή��A�o�^�[�Ƃ��Ă���A�C�e���̑Ή��A�o�^�[���炱�̃A�o�^�[���폜���܂����H", CurrentLanguage),
                            Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result2 == DialogResult.Yes)
                        {
                            foreach (var item2 in Items)
                            {
                                item2.SupportedAvatar = item2.SupportedAvatar.Where(avatar => avatar != item.ItemPath)
                                    .ToArray();
                            }
                        }

                        if (CommonAvatars.Any(commonAvatar => commonAvatar.Avatars.Contains(item.ItemPath)))
                        {
                            var result3 = MessageBox.Show(Helper.Translate("���̃A�o�^�[�����ʑf�̃O���[�v����폜���܂����H", CurrentLanguage),
                                Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            if (result3 == DialogResult.Yes)
                            {
                                foreach (var commonAvatar in CommonAvatars)
                                {
                                    commonAvatar.Avatars = commonAvatar.Avatars.Where(avatar => avatar != item.ItemPath)
                                        .ToArray();
                                }

                                Helper.SaveCommonAvatarData(CommonAvatars);
                            }
                        }

                    }

                    MessageBox.Show(Helper.Translate("�폜���������܂����B", CurrentLanguage),
                        Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);

                    if (_isSearching)
                    {
                        GenerateAvatarList();
                        GenerateAuthorList();
                        GenerateCategoryListLeft();

                        // �t�H���_�[�������̎�
                        if (_openingWindow is Window.ItemFolderCategoryList or Window.ItemFolderItemsList)
                        {
                            // �I�����ꂽ�A�o�^�[���猻�݂̏��܂ŗ��Ă�ꍇ
                            if (undo2)
                            {
                                SearchBox.Text = "";
                                SearchResultLabel.Text = "";
                                _isSearching = false;
                                ResetAvatarList(true);
                                PathTextBox.Text = GeneratePath();
                                Helper.SaveItemsData(Items);
                                return;
                            }

                            // �A�C�e���Ƃ��đI������Ă���ꍇ
                            if (undo)
                            {
                                SearchBox.Text = "";
                                SearchResultLabel.Text = "";
                                _isSearching = false;
                                GenerateItems();
                                PathTextBox.Text = GeneratePath();
                                Helper.SaveItemsData(Items);
                            }
                        }
                        else
                        {
                            SearchItems();
                            Helper.SaveItemsData(Items);
                        }
                    }
                    else
                    {
                        GenerateAvatarList();
                        GenerateAuthorList();
                        GenerateCategoryListLeft();

                        // �A�o�^�[���I�����ꂽ���(CurrentSelectedAvatarPath�Ƃ��Đݒ肳��Ă��鎞)
                        if (undo2)
                        {
                            ResetAvatarList(true);
                            PathTextBox.Text = GeneratePath();
                            Helper.SaveItemsData(Items);
                            return;
                        }

                        // �t�H���_�[���J���Ă����āA�A�C�e�����I�����ꂽ���(CurrentSelectedItem�Ƃ��Đݒ肳��Ă��鎞)
                        if (undo)
                        {
                            GenerateItems();
                            PathTextBox.Text = GeneratePath();
                            Helper.SaveItemsData(Items);
                            return;
                        }

                        // �A�C�e����ʂɊ��ɂ���
                        if (_openingWindow == Window.ItemList)
                        {
                            GenerateItems();
                            Helper.SaveItemsData(Items);
                            return;
                        }

                        // �A�C�e����ʂ̑O�ɂ���
                        RefleshWindow();

                        Helper.SaveItemsData(Items);
                    }
                };

                toolStripMenuItem6.Click += clickEvent8;
                toolStripMenuItem6.Disposed += (_, _) => toolStripMenuItem6.Click -= clickEvent8;

                contextMenuStrip.Items.Add(toolStripMenuItem2);
                contextMenuStrip.Items.Add(toolStripMenuItem3);
                contextMenuStrip.Items.Add(toolStripMenuItem4);
                contextMenuStrip.Items.Add(toolStripMenuItem5);
                contextMenuStrip.Items.Add(toolStripMenuItem6);
                button.ContextMenuStrip = contextMenuStrip;

                AvatarPage.Controls.Add(button);
                index++;
            }
        }

        private void GenerateAuthorList()
        {
            ResetAvatarPage(AvatarAuthorPage);

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

            if (authors.Length == 0) return;
            authors = authors.OrderBy(author => author.AuthorName).ToArray();

            foreach (var author in authors)
            {
                Button button = Helper.CreateButton(author.AuthorImagePath, author.AuthorName,
                    Items.Count(item => item.AuthorName == author.AuthorName) +
                    Helper.Translate("�̍���", CurrentLanguage), true, author.AuthorName, GetAvatarListWidth());
                button.Location = new Point(0, (70 * index) + 2);
                EventHandler clickEvent = (_, _) =>
                {
                    CurrentPath = new CurrentPath
                    {
                        CurrentSelectedAuthor = author
                    };

                    _authorMode = true;
                    _categoryMode = false;
                    SearchBox.Text = "";
                    SearchResultLabel.Text = "";
                    _isSearching = false;
                    GenerateCategoryList();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) =>
                {
                    button.Click -= clickEvent;
                    button.ContextMenuStrip?.Dispose();
                };

                ContextMenuStrip contextMenuStrip = new();

                ToolStripMenuItem toolStripMenuItem = new(Helper.Translate("�T���l�C���ύX", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent1 = (_, _) =>
                {
                    OpenFileDialog ofd = new()
                    {
                        Filter = Helper.Translate("�摜�t�@�C��|*.png;*.jpg", CurrentLanguage),
                        Title = Helper.Translate("�T���l�C���ύX", CurrentLanguage),
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() != DialogResult.OK) return;
                    MessageBox.Show(
                        Helper.Translate("�T���l�C����ύX���܂����I", CurrentLanguage) + "\n\n" +
                        Helper.Translate("�ύX�O: ", CurrentLanguage) + author.AuthorImagePath + "\n\n" +
                        Helper.Translate("�ύX��: ", CurrentLanguage) + ofd.FileName,
                        "����", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    foreach (var item in Items.Where(item => item.AuthorName == author.AuthorName))
                    {
                        item.AuthorImageFilePath = ofd.FileName;
                    }

                    GenerateAuthorList();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem.Click += clickEvent1;
                toolStripMenuItem.Disposed += (_, _) => toolStripMenuItem.Click -= clickEvent1;

                contextMenuStrip.Items.Add(toolStripMenuItem);
                button.ContextMenuStrip = contextMenuStrip;
                AvatarAuthorPage.Controls.Add(button);
                index++;
            }
        }

        private void GenerateCategoryListLeft()
        {
            ResetAvatarPage(CategoryPage);

            var index = 0;
            foreach (ItemType itemType in Enum.GetValues(typeof(ItemType)))
            {
                if (itemType is ItemType.Unknown or ItemType.Custom) continue;

                var items = Items.Where(item => item.Type == itemType);
                var itemCount = items.Count();
                Button button = Helper.CreateButton(null,
                    Helper.GetCategoryName(itemType, CurrentLanguage),
                    itemCount + Helper.Translate("�̍���", CurrentLanguage), true, "", GetAvatarListWidth());
                button.Location = new Point(0, (70 * index) + 2);
                EventHandler clickEvent = (_, _) =>
                {
                    CurrentPath = new CurrentPath
                    {
                        CurrentSelectedCategory = itemType
                    };

                    _authorMode = false;
                    _categoryMode = true;
                    SearchBox.Text = "";
                    SearchResultLabel.Text = "";
                    _isSearching = false;
                    GenerateItems();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) => button.Click -= clickEvent;

                CategoryPage.Controls.Add(button);
                index++;
            }

            if (CustomCategories.Length == 0) return;

            foreach (var customCategory in CustomCategories)
            {
                var items = Items.Where(item => item.CustomCategory == customCategory);
                var itemCount = items.Count();

                Button button = Helper.CreateButton(null, customCategory,
                    itemCount + Helper.Translate("�̍���", CurrentLanguage), true, "", GetAvatarListWidth());
                button.Location = new Point(0, (70 * index) + 2);
                EventHandler clickEvent = (_, _) =>
                {
                    CurrentPath = new CurrentPath
                    {
                        CurrentSelectedCategory = ItemType.Custom,
                        CurrentSelectedCustomCategory = customCategory
                    };
                    _authorMode = false;
                    _categoryMode = true;
                    SearchBox.Text = "";
                    SearchResultLabel.Text = "";
                    _isSearching = false;
                    GenerateItems();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) => button.Click -= clickEvent;

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
                if (itemType is ItemType.Unknown or ItemType.Custom) continue;

                int itemCount = 0;
                if (_authorMode)
                {
                    itemCount = Items.Count(item =>
                        item.Type == itemType &&
                        item.AuthorName == CurrentPath.CurrentSelectedAuthor?.AuthorName
                    );
                }
                else
                {
                    itemCount = Items.Count(item =>
                        item.Type == itemType &&
                        (
                            Helper.IsSupportedAvatarOrCommon(item, CommonAvatars, CurrentPath.CurrentSelectedAvatarPath)
                                .IsSupportedOrCommon ||
                            item.SupportedAvatar.Length == 0 || CurrentPath.CurrentSelectedAvatar == "*"
                        )
                    );
                }

                if (itemCount == 0) continue;

                Button button = Helper.CreateButton(null,
                    Helper.GetCategoryName(itemType, CurrentLanguage),
                    itemCount + Helper.Translate("�̍���", CurrentLanguage), false, "", GetItemExplorerListWidth());
                button.Location = new Point(0, (70 * index) + 2);

                EventHandler clickEvent = (_, _) =>
                {
                    CurrentPath.CurrentSelectedCategory = itemType;
                    GenerateItems();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) => button.Click -= clickEvent;

                AvatarItemExplorer.Controls.Add(button);
                index++;
            }

            if (CustomCategories.Length == 0) return;
            foreach (var customCategory in CustomCategories)
            {
                var itemCount = 0;
                if (_authorMode)
                {
                    itemCount = Items.Count(item =>
                        item.CustomCategory == customCategory &&
                        item.AuthorName == CurrentPath.CurrentSelectedAuthor?.AuthorName
                    );
                }
                else
                {
                    itemCount = Items.Count(item =>
                        item.CustomCategory == customCategory &&
                        (
                            Helper.IsSupportedAvatarOrCommon(item, CommonAvatars, CurrentPath.CurrentSelectedAvatarPath)
                                .IsSupportedOrCommon ||
                            item.SupportedAvatar.Length == 0 || CurrentPath.CurrentSelectedAvatar == "*"
                        )
                    );
                }

                if (itemCount == 0) continue;

                Button button = Helper.CreateButton(null, customCategory,
                    itemCount + Helper.Translate("�̍���", CurrentLanguage), false, "", GetItemExplorerListWidth());
                button.Location = new Point(0, (70 * index) + 2);
                EventHandler clickEvent = (_, _) =>
                {
                    CurrentPath.CurrentSelectedCategory = ItemType.Custom;
                    CurrentPath.CurrentSelectedCustomCategory = customCategory;
                    GenerateItems();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) => button.Click -= clickEvent;

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
                    item.Type == CurrentPath.CurrentSelectedCategory && (item.Type != ItemType.Custom || item.CustomCategory == CurrentPath.CurrentSelectedCustomCategory) &&
                    item.AuthorName == CurrentPath.CurrentSelectedAuthor?.AuthorName
                );
            }
            else if (_categoryMode)
            {
                filteredItems = Items.Where(item =>
                    item.Type == CurrentPath.CurrentSelectedCategory && (item.Type != ItemType.Custom || item.CustomCategory == CurrentPath.CurrentSelectedCustomCategory)
                );
            }
            else
            {
                filteredItems = Items.Where(item =>
                    item.Type == CurrentPath.CurrentSelectedCategory && (item.Type != ItemType.Custom || item.CustomCategory == CurrentPath.CurrentSelectedCustomCategory) &&
                    (
                        Helper.IsSupportedAvatarOrCommon(item, CommonAvatars, CurrentPath.CurrentSelectedAvatarPath)
                            .IsSupportedOrCommon ||
                        item.SupportedAvatar.Length == 0 || CurrentPath.CurrentSelectedAvatar == "*"
                    )
                );
            }

            filteredItems = filteredItems.OrderBy(item =>
            {
                var mode = SortingBox.SelectedIndex;
                return mode switch
                {
                    0 => item.Title,
                    1 => item.AuthorName,
                    _ => item.Title
                };
            }).ToList();
            if (!filteredItems.Any()) return;

            var index = 0;
            foreach (Item item in filteredItems)
            {
                var authorText = Helper.Translate("���: ", CurrentLanguage) + item.AuthorName;

                var isSupportedOrCommon =
                    Helper.IsSupportedAvatarOrCommon(item, CommonAvatars, CurrentPath.CurrentSelectedAvatarPath);

                if (isSupportedOrCommon.OnlyCommon && item.SupportedAvatar.Length != 0 &&
                    !item.SupportedAvatar.Contains(CurrentPath.CurrentSelectedAvatarPath))
                {
                    var commonAvatarName = isSupportedOrCommon.CommonAvatarName;
                    if (!string.IsNullOrEmpty(commonAvatarName))
                    {
                        authorText += "\n" + Helper.Translate("���ʑf��: ", CurrentLanguage) + commonAvatarName;
                    }
                }

                var description = item.Title;
                if (!string.IsNullOrEmpty(item.ItemMemo))
                {
                    description += "\n\n" + Helper.Translate("����: ", CurrentLanguage) + item.ItemMemo;
                }

                Button button = Helper.CreateButton(item.ImagePath, item.Title, authorText, false, description,
                    GetItemExplorerListWidth());
                button.Location = new Point(0, (70 * index) + 2);
                EventHandler clickEvent = (_, _) =>
                {
                    if (!Directory.Exists(item.ItemPath))
                    {
                        var prePath = item.ItemPath;
                        DialogResult result =
                            MessageBox.Show(Helper.Translate("�t�H���_��������܂���ł����B�ҏW���܂����H", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.YesNo,
                                MessageBoxIcon.Error);
                        if (result != DialogResult.Yes) return;

                        AddItem addItem = new(this, CurrentPath.CurrentSelectedCategory, CurrentPath.CurrentSelectedCustomCategory, true, item, null);
                        addItem.ShowDialog();

                        if (!Directory.Exists(item.ItemPath))
                        {
                            MessageBox.Show(Helper.Translate("�t�H���_��������܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        //�Ή��A�o�^�[�̃p�X��ς��Ă�����
                        foreach (var item2 in Items)
                        {
                            if (item2.SupportedAvatar.Contains(prePath))
                            {
                                item2.SupportedAvatar = item2.SupportedAvatar.Select(avatar =>
                                    avatar == prePath ? item.ItemPath : avatar).ToArray();
                            }
                        }

                        if (CurrentPath.CurrentSelectedAvatarPath == prePath)
                        {
                            CurrentPath.CurrentSelectedAvatar = item.Title;
                            CurrentPath.CurrentSelectedAvatarPath = item.ItemPath;
                        }

                        RefleshWindow();
                        Helper.SaveItemsData(Items);
                    }

                    CurrentPath.CurrentSelectedItem = item;
                    GenerateItemCategoryList();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) => button.Click -= clickEvent;

                ContextMenuStrip contextMenuStrip = new();

                if (Directory.Exists(item.ItemPath))
                {
                    ToolStripMenuItem toolStripMenuItem = new(Helper.Translate("�t�H���_���J��", CurrentLanguage),
                        SharedImages.GetImage(SharedImages.Images.OpenIcon));
                    EventHandler clickEvent1 = (_, _) =>
                    {
                        if (!Directory.Exists(item.ItemPath))
                        {
                            MessageBox.Show(Helper.Translate("�t�H���_��������܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        try
                        {
                            Process.Start("explorer.exe", item.ItemPath);
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�t�H���_���J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    toolStripMenuItem.Click += clickEvent1;
                    toolStripMenuItem.Disposed += (_, _) => toolStripMenuItem.Click -= clickEvent1;

                    contextMenuStrip.Items.Add(toolStripMenuItem);
                }

                if (item.BoothId != -1)
                {
                    ToolStripMenuItem toolStripMenuItem =
                        new(Helper.Translate("Booth�����N�̃R�s�[", CurrentLanguage),
                            SharedImages.GetImage(SharedImages.Images.CopyIcon));
                    EventHandler clickEvent2 = (_, _) =>
                    {
                        try
                        {
                            Clipboard.SetText(
                                $"https://booth.pm/{Helper.GetCurrentLanguageCode(CurrentLanguage)}/items/" +
                                item.BoothId);
                        }
                        catch (Exception ex)
                        {
                            if (ex is ExternalException) return;
                            MessageBox.Show(Helper.Translate("�N���b�v�{�[�h�ɃR�s�[�ł��܂���ł���", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    toolStripMenuItem.Click += clickEvent2;
                    toolStripMenuItem.Disposed += (_, _) => toolStripMenuItem.Click -= clickEvent2;

                    ToolStripMenuItem toolStripMenuItem1 =
                        new(Helper.Translate("Booth�����N���J��", CurrentLanguage),
                            SharedImages.GetImage(SharedImages.Images.CopyIcon));
                    EventHandler clickEvent3 = (_, _) =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = $"https://booth.pm/{Helper.GetCurrentLanguageCode(CurrentLanguage)}/items/" +
                                           item.BoothId,
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�����N���J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    toolStripMenuItem1.Click += clickEvent3;
                    toolStripMenuItem1.Disposed += (_, _) => toolStripMenuItem1.Click -= clickEvent3;

                    contextMenuStrip.Items.Add(toolStripMenuItem);
                    contextMenuStrip.Items.Add(toolStripMenuItem1);
                }

                ToolStripMenuItem toolStripMenuItem2 = new(Helper.Translate("���̍�҂̑��̃A�C�e����\��", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.OpenIcon));
                EventHandler clickEvent4 = (_, _) =>
                {
                    SearchBox.Text = $"Author=\"{item.AuthorName}\"";
                    SearchItems();
                };

                toolStripMenuItem2.Click += clickEvent4;
                toolStripMenuItem2.Disposed += (_, _) => toolStripMenuItem2.Click -= clickEvent4;

                ToolStripMenuItem toolStripMenuItem3 = new(Helper.Translate("�T���l�C���ύX", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent5 = (_, _) =>
                {
                    OpenFileDialog ofd = new()
                    {
                        Filter = Helper.Translate("�摜�t�@�C��|*.png;*.jpg", CurrentLanguage),
                        Title = Helper.Translate("�T���l�C���ύX", CurrentLanguage),
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() != DialogResult.OK) return;
                    MessageBox.Show(
                        Helper.Translate("�T���l�C����ύX���܂����I", CurrentLanguage) + "\n\n" +
                        Helper.Translate("�ύX�O: ", CurrentLanguage) + item.ImagePath + "\n\n" +
                        Helper.Translate("�ύX��: ", CurrentLanguage) + ofd.FileName,
                        Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    item.ImagePath = ofd.FileName;

                    if (_isSearching)
                    {
                        SearchItems();
                    }
                    else
                    {
                        GenerateItems();
                    }

                    GenerateAvatarList();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem3.Click += clickEvent5;
                toolStripMenuItem3.Disposed += (_, _) => toolStripMenuItem3.Click -= clickEvent5;

                ToolStripMenuItem toolStripMenuItem4 = new(Helper.Translate("�ҏW", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent6 = (_, _) =>
                {
                    var prePath = item.ItemPath;

                    AddItem addItem = new(this, CurrentPath.CurrentSelectedCategory, CurrentPath.CurrentSelectedCustomCategory, true, item, null);
                    addItem.ShowDialog();

                    //�Ή��A�o�^�[�̃p�X��ς��Ă�����
                    foreach (var item2 in Items)
                    {
                        if (item2.SupportedAvatar.Contains(prePath))
                        {
                            item2.SupportedAvatar = item2.SupportedAvatar.Select(avatar =>
                                avatar == prePath ? item.ItemPath : avatar).ToArray();
                        }
                    }

                    if (CurrentPath.CurrentSelectedAvatarPath == prePath)
                    {
                        CurrentPath.CurrentSelectedAvatar = item.Title;
                        CurrentPath.CurrentSelectedAvatarPath = item.ItemPath;
                    }

                    if (!_isSearching) PathTextBox.Text = GeneratePath();
                    RefleshWindow();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem4.Click += clickEvent6;
                toolStripMenuItem4.Disposed += (_, _) => toolStripMenuItem4.Click -= clickEvent6;

                ToolStripMenuItem toolStripMenuItem5 = new(Helper.Translate("�����̒ǉ�", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent7 = (_, _) =>
                {
                    var previouseMemo = item.ItemMemo;
                    AddNote addMemo = new(this, item);
                    addMemo.ShowDialog();

                    var memo = addMemo.Memo;
                    if (string.IsNullOrEmpty(memo) || memo == previouseMemo) return;

                    item.ItemMemo = memo;

                    GenerateAuthorList();
                    GenerateItems();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem5.Click += clickEvent7;
                toolStripMenuItem5.Disposed += (_, _) => toolStripMenuItem5.Click -= clickEvent7;

                ToolStripMenuItem toolStripMenuItem6 = new(Helper.Translate("�폜", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.TrashIcon));
                EventHandler clickEvent8 = (_, _) =>
                {
                    DialogResult result = MessageBox.Show(Helper.Translate("�{���ɍ폜���܂����H", CurrentLanguage),
                        Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) return;

                    var undo = false;
                    if (CurrentPath.CurrentSelectedAvatarPath == item.ItemPath && !_authorMode && !_categoryMode)
                    {
                        CurrentPath = new CurrentPath();
                        undo = true;
                        PathTextBox.Text = GeneratePath();
                    }

                    Items = Items.Where(i => i.ItemPath != item.ItemPath).ToArray();

                    if (item.Type == ItemType.Avatar)
                    {
                        var result2 = MessageBox.Show(
                            Helper.Translate("���̃A�o�^�[��Ή��A�o�^�[�Ƃ��Ă���A�C�e���̑Ή��A�o�^�[���炱�̃A�o�^�[���폜���܂����H", CurrentLanguage),
                            Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result2 == DialogResult.Yes)
                        {
                            foreach (var item2 in Items)
                            {
                                item2.SupportedAvatar = item2.SupportedAvatar.Where(avatar => avatar != item.ItemPath)
                                    .ToArray();
                            }
                        }

                        if (CommonAvatars.Any(commonAvatar => commonAvatar.Avatars.Contains(item.ItemPath)))
                        {
                            var result3 = MessageBox.Show(Helper.Translate("���̃A�o�^�[�����ʑf�̃O���[�v����폜���܂����H", CurrentLanguage),
                                Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            if (result3 == DialogResult.Yes)
                            {
                                foreach (var commonAvatar in CommonAvatars)
                                {
                                    commonAvatar.Avatars = commonAvatar.Avatars.Where(avatar => avatar != item.ItemPath)
                                        .ToArray();
                                }

                                Helper.SaveCommonAvatarData(CommonAvatars);
                            }
                        }
                    }

                    Items = Items.Where(i => i.ItemPath != item.ItemPath).ToArray();
                    MessageBox.Show(Helper.Translate("�폜���������܂����B", CurrentLanguage),
                        Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);

                    if (undo)
                    {
                        SearchBox.Text = "";
                        SearchResultLabel.Text = "";
                        _isSearching = false;
                        GenerateAvatarList();
                        GenerateAuthorList();
                        GenerateCategoryListLeft();
                        ResetAvatarList(true);
                    }
                    else
                    {
                        RefleshWindow();
                    }

                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem6.Click += clickEvent8;
                toolStripMenuItem6.Disposed += (_, _) => toolStripMenuItem6.Click -= clickEvent8;

                contextMenuStrip.Items.Add(toolStripMenuItem2);
                contextMenuStrip.Items.Add(toolStripMenuItem3);
                contextMenuStrip.Items.Add(toolStripMenuItem4);
                contextMenuStrip.Items.Add(toolStripMenuItem5);
                contextMenuStrip.Items.Add(toolStripMenuItem6);
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
                "�}�e���A��",
                "�s��"
            };
            if (CurrentPath.CurrentSelectedItem == null) return;
            ItemFolderInfo itemFolderInfo = Helper.GetItemFolderInfo(CurrentPath.CurrentSelectedItem.ItemPath,
                CurrentPath.CurrentSelectedItem.MaterialPath);
            CurrentPath.CurrentSelectedItemFolderInfo = itemFolderInfo;

            ResetAvatarList();

            var index = 0;
            foreach (var itemType in types)
            {
                var itemCount = itemFolderInfo.GetItemCount(itemType);
                if (itemCount == 0) continue;

                Button button = Helper.CreateButton(null,
                    Helper.Translate(itemType, CurrentLanguage), itemCount + Helper.Translate("�̍���", CurrentLanguage),
                    false, "", GetItemExplorerListWidth());
                button.Location = new Point(0, (70 * index) + 2);
                EventHandler clickEvent = (_, _) =>
                {
                    CurrentPath.CurrentSelectedItemCategory = itemType;
                    GenerateItemFiles();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) => button.Click -= clickEvent;

                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        private void GenerateItemFiles()
        {
            _openingWindow = Window.ItemFolderItemsList;
            ResetAvatarList();

            var files = CurrentPath.CurrentSelectedItemFolderInfo.GetItems(CurrentPath.CurrentSelectedItemCategory);
            if (files.Length == 0) return;

            files = files.OrderBy(file => file.FileName).ToArray();

            var index = 0;
            foreach (var file in files)
            {
                var imagePath = file.FileExtension is ".png" or ".jpg" ? file.FilePath : "";
                Button button = Helper.CreateButton(imagePath, file.FileName,
                    file.FileExtension.Replace(".", "") + Helper.Translate("�t�@�C��", CurrentLanguage), false,
                    Helper.Translate("�J���t�@�C���̃p�X: ", CurrentLanguage) + file.FilePath, GetItemExplorerListWidth());
                button.Location = new Point(0, (70 * index) + 2);

                ContextMenuStrip contextMenuStrip = new();

                ToolStripMenuItem toolStripMenuItem = new(Helper.Translate("�J��", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.CopyIcon));
                EventHandler clickEvent = (_, _) =>
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
                        try
                        {
                            Process.Start("explorer.exe", "/select," + file.FilePath);
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�t�@�C�����J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                };

                toolStripMenuItem.Click += clickEvent;
                toolStripMenuItem.Disposed += (_, _) => toolStripMenuItem.Click -= clickEvent;

                ToolStripMenuItem toolStripMenuItem1 = new(Helper.Translate("�t�@�C���̃p�X���J��", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.CopyIcon));
                EventHandler clickEvent1 = (_, _) =>
                {
                    try
                    {
                        Process.Start("explorer.exe", "/select," + file.FilePath);
                    }
                    catch
                    {
                        MessageBox.Show(Helper.Translate("�t�@�C�����J���܂���ł����B", CurrentLanguage),
                            Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                };

                toolStripMenuItem1.Click += clickEvent1;
                toolStripMenuItem1.Disposed += (_, _) => toolStripMenuItem1.Click -= clickEvent1;

                contextMenuStrip.Items.Add(toolStripMenuItem);
                contextMenuStrip.Items.Add(toolStripMenuItem1);
                button.ContextMenuStrip = contextMenuStrip;

                EventHandler clickEvent2 = (_, _) =>
                {
                    try
                    {
                        if (file.FileExtension is ".unitypackage")
                        {
                            _ = Helper.ModifyUnityPackageFilePathAsync(file, CurrentPath, CurrentLanguage);
                        }
                        else
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = file.FilePath,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch
                    {
                        try
                        {
                            Process.Start("explorer.exe", "/select," + file.FilePath);
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�t�@�C�����J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                };

                button.Click += clickEvent2;
                button.Disposed += (_, _) => button.Click -= clickEvent2;

                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        private void GenerateFilteredItem(SearchFilter searchFilter)
        {
            ResetAvatarList();

            var filteredItems = Items.Where(item =>
            {
                if (searchFilter.Author.Length != 0 && !searchFilter.Author.Contains(item.AuthorName))
                    return false;

                if (searchFilter.Title.Length != 0 && !searchFilter.Title.Contains(item.Title))
                    return false;

                if (searchFilter.BoothId.Length != 0 && !searchFilter.BoothId.Contains(item.BoothId.ToString()))
                    return false;

                if (searchFilter.Avatar.Length != 0 && !searchFilter.Avatar.Any(avatar =>
                {
                    return item.SupportedAvatar.Any(supportedAvatar =>
                    {
                        var supportedAvatarName = Helper.GetAvatarNameFromPath(Items, supportedAvatar);
                        if (supportedAvatarName == "") return false;
                        return supportedAvatarName.ToLower().Contains(avatar.ToLower());
                    });
                }))
                {
                    return false;
                }

                if (searchFilter.Category.Length != 0 && !searchFilter.Category.Any(category =>
                {
                    var translatedCategory = Helper.GetCategoryName(item.Type, CurrentLanguage);
                    return translatedCategory.Contains(category) || item.CustomCategory.Contains(category);

                }))
                {
                    return false;
                }

                if (searchFilter.ItemMemo.Length != 0 && !searchFilter.ItemMemo.Any(memo =>
                    {
                        return item.ItemMemo.ToLower().Contains(memo.ToLower());
                    }))
                {
                    return false;
                }

                return true;
            });

            filteredItems = filteredItems
                .Where(item =>
                    searchFilter.SearchWords.All(word =>
                        item.Title.ToLower().Contains(word.ToLower()) ||
                        item.AuthorName.ToLower().Contains(word.ToLower()) ||
                        item.SupportedAvatar.Any(avatar =>
                        {
                            var supportedAvatarName = Helper.GetAvatarNameFromPath(Items, avatar);
                            if (supportedAvatarName == "") return false;
                            return supportedAvatarName.ToLower().Contains(word.ToLower());
                        }) ||
                        item.BoothId.ToString().Contains(word.ToLower()) ||
                        item.ItemMemo.ToLower().Contains(word.ToLower())
                    )
                )
                .OrderByDescending(item =>
                {
                    var matchCount = 0;
                    foreach (var word in searchFilter.SearchWords)
                    {
                        if (item.Title.ToLower().Contains(word.ToLower())) matchCount++;
                        if (item.AuthorName.ToLower().Contains(word.ToLower())) matchCount++;
                        if (item.SupportedAvatar.Any(avatar =>
                        {
                            var supportedAvatarName = Helper.GetAvatarNameFromPath(Items, avatar);
                            if (supportedAvatarName == "") return false;
                            return supportedAvatarName.ToLower().Contains(word.ToLower());
                        })) matchCount++;
                        if (item.BoothId.ToString().Contains(word.ToLower())) matchCount++;
                        if (item.ItemMemo.ToLower().Contains(word.ToLower())) matchCount++;
                    }

                    return matchCount;
                })
                .ToList();

            SearchResultLabel.Text = Helper.Translate("��������: ", CurrentLanguage) + filteredItems.Count() +
                                     Helper.Translate("��", CurrentLanguage) + Helper.Translate(" (�S", CurrentLanguage) +
                                     Items.Length + Helper.Translate("��)", CurrentLanguage);
            if (!filteredItems.Any()) return;

            var index = 0;
            foreach (Item item in filteredItems)
            {
                var description = item.Title;
                if (!string.IsNullOrEmpty(item.ItemMemo))
                {
                    description += "\n\n" + Helper.Translate("����: ", CurrentLanguage) + item.ItemMemo;
                }

                Button button = Helper.CreateButton(item.ImagePath, item.Title,
                    Helper.Translate("���: ", CurrentLanguage) + item.AuthorName, false,
                    description, GetItemExplorerListWidth());
                button.Location = new Point(0, (70 * index) + 2);
                EventHandler clickEvent = (_, _) =>
                {
                    if (!Directory.Exists(item.ItemPath))
                    {
                        var prePath = item.ItemPath;
                        DialogResult result =
                            MessageBox.Show(Helper.Translate("�t�H���_��������܂���ł����B�ҏW���܂����H", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.YesNo,
                                MessageBoxIcon.Error);
                        if (result != DialogResult.Yes) return;

                        AddItem addItem = new(this, CurrentPath.CurrentSelectedCategory, CurrentPath.CurrentSelectedCustomCategory, true, item, null);
                        addItem.ShowDialog();

                        if (!Directory.Exists(item.ItemPath))
                        {
                            MessageBox.Show(Helper.Translate("�t�H���_��������܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        //�Ή��A�o�^�[�̃p�X��ς��Ă�����
                        foreach (var item2 in Items)
                        {
                            if (item2.SupportedAvatar.Contains(prePath))
                            {
                                item2.SupportedAvatar = item2.SupportedAvatar.Select(avatar =>
                                    avatar == prePath ? item.ItemPath : avatar).ToArray();
                            }
                        }

                        GenerateFilteredItem(searchFilter);
                        GenerateAvatarList();
                        GenerateAuthorList();
                        GenerateCategoryListLeft();
                        Helper.SaveItemsData(Items);
                    }

                    _authorMode = false;
                    _categoryMode = false;
                    GeneratePathFromItem(item);
                    SearchBox.Text = "";
                    SearchResultLabel.Text = "";
                    _isSearching = false;
                    GenerateItemCategoryList();
                    PathTextBox.Text = GeneratePath();
                };

                button.Click += clickEvent;
                button.Disposed += (_, _) =>
                {
                    button.Click -= clickEvent;
                    button.ContextMenuStrip?.Dispose();
                };

                ContextMenuStrip contextMenuStrip = new();

                if (Directory.Exists(item.ItemPath))
                {
                    ToolStripMenuItem toolStripMenuItem = new(Helper.Translate("�t�H���_���J��", CurrentLanguage),
                        SharedImages.GetImage(SharedImages.Images.OpenIcon));
                    EventHandler clickEvent1 = (_, _) =>
                    {
                        if (!Directory.Exists(item.ItemPath))
                        {
                            MessageBox.Show(Helper.Translate("�t�H���_��������܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        try
                        {
                            Process.Start("explorer.exe", item.ItemPath);
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�t�H���_���J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    toolStripMenuItem.Click += clickEvent1;
                    toolStripMenuItem.Disposed += (_, _) => toolStripMenuItem.Click -= clickEvent1;

                    contextMenuStrip.Items.Add(toolStripMenuItem);
                }

                if (item.BoothId != -1)
                {
                    ToolStripMenuItem toolStripMenuItem =
                        new(Helper.Translate("Booth�����N�̃R�s�[", CurrentLanguage),
                            SharedImages.GetImage(SharedImages.Images.CopyIcon));
                    EventHandler clickEvent2 = (_, _) =>
                    {
                        try
                        {
                            Clipboard.SetText(
                                $"https://booth.pm/{Helper.GetCurrentLanguageCode(CurrentLanguage)}/items/" +
                                item.BoothId);
                        }
                        catch (Exception ex)
                        {
                            if (ex is ExternalException) return;
                            MessageBox.Show(Helper.Translate("�N���b�v�{�[�h�ɃR�s�[�ł��܂���ł���", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    toolStripMenuItem.Click += clickEvent2;
                    toolStripMenuItem.Disposed += (_, _) => toolStripMenuItem.Click -= clickEvent2;

                    ToolStripMenuItem toolStripMenuItem1 =
                        new(Helper.Translate("Booth�����N���J��", CurrentLanguage),
                            SharedImages.GetImage(SharedImages.Images.CopyIcon));
                    EventHandler clickEvent3 = (_, _) =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = $"https://booth.pm/{Helper.GetCurrentLanguageCode(CurrentLanguage)}/items/" +
                                           item.BoothId,
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�����N���J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    };

                    toolStripMenuItem1.Click += clickEvent3;
                    toolStripMenuItem1.Disposed += (_, _) => toolStripMenuItem1.Click -= clickEvent3;

                    contextMenuStrip.Items.Add(toolStripMenuItem);
                    contextMenuStrip.Items.Add(toolStripMenuItem1);
                }

                ToolStripMenuItem toolStripMenuItem2 = new(Helper.Translate("���̍�҂̑��̃A�C�e����\��", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.OpenIcon));
                EventHandler clickEvent4 = (_, _) =>
                {
                    SearchBox.Text = $"Author=\"{item.AuthorName}\"";
                    SearchItems();
                };

                toolStripMenuItem2.Click += clickEvent4;
                toolStripMenuItem2.Disposed += (_, _) => toolStripMenuItem2.Click -= clickEvent4;

                ToolStripMenuItem toolStripMenuItem3 = new(Helper.Translate("�T���l�C���ύX", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent5 = (_, _) =>
                {
                    OpenFileDialog ofd = new()
                    {
                        Filter = Helper.Translate("�摜�t�@�C��|*.png;*.jpg", CurrentLanguage),
                        Title = Helper.Translate("�T���l�C���ύX", CurrentLanguage),
                        Multiselect = false
                    };

                    if (ofd.ShowDialog() != DialogResult.OK) return;
                    MessageBox.Show(
                        Helper.Translate("�T���l�C����ύX���܂����I", CurrentLanguage) + "\n\n" +
                        Helper.Translate("�ύX�O: ", CurrentLanguage) + item.ImagePath + "\n\n" +
                        Helper.Translate("�ύX��: ", CurrentLanguage) + ofd.FileName,
                        Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    item.ImagePath = ofd.FileName;
                    GenerateFilteredItem(searchFilter);
                    GenerateAvatarList();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem3.Click += clickEvent5;
                toolStripMenuItem3.Disposed += (_, _) => toolStripMenuItem3.Click -= clickEvent5;

                ToolStripMenuItem toolStripMenuItem4 = new(Helper.Translate("�ҏW", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent6 = (_, _) =>
                {
                    var prePath = item.ItemPath;
                    AddItem addItem = new(this, item.Type, item.CustomCategory, true, item, null);
                    addItem.ShowDialog();

                    //�Ή��A�o�^�[�̃p�X��ς��Ă�����
                    foreach (var item2 in Items)
                    {
                        if (item2.SupportedAvatar.Contains(prePath))
                        {
                            item2.SupportedAvatar = item2.SupportedAvatar.Select(avatar =>
                                avatar == prePath ? item.ItemPath : avatar).ToArray();
                        }
                    }

                    if (CurrentPath.CurrentSelectedAvatarPath == prePath)
                    {
                        CurrentPath.CurrentSelectedAvatar = item.Title;
                        CurrentPath.CurrentSelectedAvatarPath = item.ItemPath;
                    }

                    GenerateFilteredItem(searchFilter);
                    GenerateAvatarList();
                    GenerateAuthorList();
                    GenerateCategoryListLeft();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem4.Click += clickEvent6;
                toolStripMenuItem4.Disposed += (_, _) => toolStripMenuItem4.Click -= clickEvent6;

                ToolStripMenuItem toolStripMenuItem5 = new(Helper.Translate("�����̒ǉ�", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.EditIcon));
                EventHandler clickEvent7 = (_, _) =>
                {
                    var previouseMemo = item.ItemMemo;
                    AddNote addMemo = new(this, item);
                    addMemo.ShowDialog();

                    var memo = addMemo.Memo;
                    if (string.IsNullOrEmpty(memo) || memo == previouseMemo) return;

                    item.ItemMemo = memo;

                    GenerateFilteredItem(searchFilter);
                    GenerateAvatarList();
                    Helper.SaveItemsData(Items);
                };

                ToolStripMenuItem toolStripMenuItem6 = new(Helper.Translate("�폜", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.TrashIcon));
                EventHandler clickEvent8 = (_, _) =>
                {
                    DialogResult result = MessageBox.Show(Helper.Translate("�{���ɍ폜���܂����H", CurrentLanguage),
                        Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes) return;

                    Items = Items.Where(i => i.ItemPath != item.ItemPath).ToArray();

                    if (item.Type == ItemType.Avatar)
                    {
                        var result2 = MessageBox.Show(
                            Helper.Translate("���̃A�o�^�[��Ή��A�o�^�[�Ƃ��Ă���A�C�e���̑Ή��A�o�^�[���炱�̃A�o�^�[���폜���܂����H", CurrentLanguage),
                            Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result2 == DialogResult.Yes)
                        {
                            foreach (var item2 in Items)
                            {
                                item2.SupportedAvatar = item2.SupportedAvatar.Where(avatar => avatar != item.ItemPath)
                                    .ToArray();
                            }
                        }

                        if (CommonAvatars.Any(commonAvatar => commonAvatar.Avatars.Contains(item.ItemPath)))
                        {
                            var result3 = MessageBox.Show(Helper.Translate("���̃A�o�^�[�����ʑf�̃O���[�v����폜���܂����H", CurrentLanguage),
                                Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo,
                                MessageBoxIcon.Question);

                            if (result3 == DialogResult.Yes)
                            {
                                foreach (var commonAvatar in CommonAvatars)
                                {
                                    commonAvatar.Avatars = commonAvatar.Avatars.Where(avatar => avatar != item.ItemPath)
                                        .ToArray();
                                }

                                Helper.SaveCommonAvatarData(CommonAvatars);
                            }
                        }
                    }

                    Items = Items.Where(i => i.ItemPath != item.ItemPath).ToArray();
                    MessageBox.Show(Helper.Translate("�폜���������܂����B", CurrentLanguage),
                        Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);

                    GenerateFilteredItem(searchFilter);
                    GenerateAvatarList();
                    GenerateAuthorList();
                    GenerateCategoryListLeft();
                    Helper.SaveItemsData(Items);
                };

                toolStripMenuItem6.Click += clickEvent8;
                toolStripMenuItem6.Disposed += (_, _) => toolStripMenuItem6.Click -= clickEvent8;

                contextMenuStrip.Items.Add(toolStripMenuItem2);
                contextMenuStrip.Items.Add(toolStripMenuItem3);
                contextMenuStrip.Items.Add(toolStripMenuItem4);
                contextMenuStrip.Items.Add(toolStripMenuItem5);
                contextMenuStrip.Items.Add(toolStripMenuItem6);
                button.ContextMenuStrip = contextMenuStrip;
                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        private void GenerateFilteredFolderItems(SearchFilter searchWords)
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
                    searchWords.SearchWords.All(word =>
                        file.FileName.ToLower().Contains(word.ToLower())
                    )
                )
                .OrderByDescending(file =>
                {
                    return searchWords.SearchWords.Count(word => file.FileName.ToLower().Contains(word.ToLower()));
                })
                .ToList();

            SearchResultLabel.Text = Helper.Translate("�t�H���_�[����������: ", CurrentLanguage) + filteredItems.Count +
                                     Helper.Translate("��", CurrentLanguage) + Helper.Translate(" (�S", CurrentLanguage) +
                                     fileDatas.Length + Helper.Translate("��)", CurrentLanguage);
            if (!filteredItems.Any()) return;

            var index = 0;
            foreach (var file in filteredItems)
            {
                var imagePath = file.FileExtension is ".png" or ".jpg" ? file.FilePath : "";
                Button button = Helper.CreateButton(imagePath, file.FileName,
                    file.FileExtension.Replace(".", "") + Helper.Translate("�t�@�C��", CurrentLanguage), false,
                    Helper.Translate("�J���t�@�C���̃p�X: ", CurrentLanguage) + file.FilePath, GetItemExplorerListWidth());
                button.Location = new Point(0, (70 * index) + 2);

                ContextMenuStrip contextMenuStrip = new();

                ToolStripMenuItem toolStripMenuItem = new(Helper.Translate("�J��", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.CopyIcon));
                EventHandler clickEvent = (_, _) =>
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
                        try
                        {
                            Process.Start("explorer.exe", "/select," + file.FilePath);
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�t�@�C�����J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                };

                toolStripMenuItem.Click += clickEvent;
                toolStripMenuItem.Disposed += (_, _) => toolStripMenuItem.Click -= clickEvent;

                ToolStripMenuItem toolStripMenuItem1 = new(Helper.Translate("�t�@�C���̃p�X���J��", CurrentLanguage),
                    SharedImages.GetImage(SharedImages.Images.CopyIcon));
                EventHandler clickEvent1 = (_, _) =>
                {
                    try
                    {
                        Process.Start("explorer.exe", "/select," + file.FilePath);
                    }
                    catch
                    {
                        MessageBox.Show(Helper.Translate("�t�@�C�����J���܂���ł����B", CurrentLanguage),
                            Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                };

                toolStripMenuItem1.Click += clickEvent1;
                toolStripMenuItem1.Disposed += (_, _) => toolStripMenuItem1.Click -= clickEvent1;

                contextMenuStrip.Items.Add(toolStripMenuItem);
                contextMenuStrip.Items.Add(toolStripMenuItem1);
                button.ContextMenuStrip = contextMenuStrip;

                EventHandler clickEvent2 = (_, _) =>
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
                        try
                        {
                            Process.Start("explorer.exe", "/select," + file.FilePath);
                        }
                        catch
                        {
                            MessageBox.Show(Helper.Translate("�t�@�C�����J���܂���ł����B", CurrentLanguage),
                                Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                };

                button.Click += clickEvent2;
                button.Disposed += (_, _) => button.Click -= clickEvent2;

                AvatarItemExplorer.Controls.Add(button);
                index++;
            }
        }

        // Add Item Form
        private void AddItemButton_Click(object sender, EventArgs e)
        {
            AddItem addItem = new AddItem(this, CurrentPath.CurrentSelectedCategory, CurrentPath.CurrentSelectedCustomCategory, false, null, null);
            addItem.ShowDialog();
            RefleshWindow();
            Helper.SaveItemsData(Items);
        }

        // Generate Path
        private string GeneratePath()
        {
            var categoryName = Helper.GetCategoryName(CurrentPath.CurrentSelectedCategory, CurrentLanguage, CurrentPath.CurrentSelectedCustomCategory);

            if (_authorMode)
            {
                if (CurrentPath.CurrentSelectedAuthor == null)
                    return Helper.Translate("�����ɂ͌��݂̃p�X���\������܂�", CurrentLanguage);
                if (CurrentPath.CurrentSelectedCategory == ItemType.Unknown)
                    return Helper.RemoveFormat(CurrentPath.CurrentSelectedAuthor.AuthorName);
                if (CurrentPath.CurrentSelectedItem == null)
                    return Helper.RemoveFormat(CurrentPath.CurrentSelectedAuthor.AuthorName) + " / " +
                           categoryName;
                if (CurrentPath.CurrentSelectedItemCategory == null)
                    return Helper.RemoveFormat(CurrentPath.CurrentSelectedAuthor.AuthorName) + " / " +
                           categoryName + " / " +
                           Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title);

                return Helper.RemoveFormat(CurrentPath.CurrentSelectedAuthor.AuthorName) + " / " +
                       categoryName + " / " +
                       Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title) + " / " +
                       Helper.Translate(CurrentPath.CurrentSelectedItemCategory, CurrentLanguage);
            }

            if (_categoryMode)
            {
                if (CurrentPath.CurrentSelectedCategory == ItemType.Unknown)
                    return Helper.Translate("�����ɂ͌��݂̃p�X���\������܂�", CurrentLanguage);
                if (CurrentPath.CurrentSelectedItem == null)
                    return categoryName;
                if (CurrentPath.CurrentSelectedItemCategory == null)
                    return categoryName + " / " +
                           Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title);

                return categoryName + " / " +
                       Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title) + " / " +
                       Helper.Translate(CurrentPath.CurrentSelectedItemCategory, CurrentLanguage);
            }

            if (CurrentPath.CurrentSelectedAvatar == null) return Helper.Translate("�����ɂ͌��݂̃p�X���\������܂�", CurrentLanguage);
            if (CurrentPath.CurrentSelectedCategory == ItemType.Unknown)
                return Helper.RemoveFormat(CurrentPath.CurrentSelectedAvatar);
            if (CurrentPath.CurrentSelectedItem == null)
                return Helper.RemoveFormat(CurrentPath.CurrentSelectedAvatar) + " / " +
                       categoryName;
            if (CurrentPath.CurrentSelectedItemCategory == null)
                return Helper.RemoveFormat(CurrentPath.CurrentSelectedAvatar) + " / " +
                       categoryName + " / " +
                       Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title);

            return Helper.RemoveFormat(CurrentPath.CurrentSelectedAvatar) + " / " +
                   categoryName + " / " +
                   Helper.RemoveFormat(CurrentPath.CurrentSelectedItem.Title) + " / " +
                   Helper.Translate(CurrentPath.CurrentSelectedItemCategory, CurrentLanguage);
        }

        private void GeneratePathFromItem(Item item)
        {
            var avatarPath = item.SupportedAvatar.FirstOrDefault();
            var avatarName = Helper.GetAvatarName(Items, avatarPath);
            CurrentPath.CurrentSelectedAvatar = avatarName ?? "*";
            CurrentPath.CurrentSelectedAvatarPath = avatarPath;
            CurrentPath.CurrentSelectedCategory = item.Type;
            if (item.Type == ItemType.Custom)
                CurrentPath.CurrentSelectedCustomCategory = item.CustomCategory;
            CurrentPath.CurrentSelectedItem = item;
        }

        // Undo Button
        private void UndoButton_Click(object sender, EventArgs e)
        {
            //�������������ꍇ�͑O�̉�ʂ܂łƂ肠�����߂��Ă�����
            if (_isSearching)
            {
                SearchBox.Text = "";
                SearchResultLabel.Text = "";
                _isSearching = false;
                if (CurrentPath.CurrentSelectedItemCategory != null)
                {
                    GenerateItemFiles();
                    PathTextBox.Text = GeneratePath();
                    return;
                }

                if (CurrentPath.CurrentSelectedItem != null)
                {
                    GenerateItemCategoryList();
                    PathTextBox.Text = GeneratePath();
                    return;
                }

                if (CurrentPath.CurrentSelectedCategory != ItemType.Unknown)
                {
                    GenerateItems();
                    PathTextBox.Text = GeneratePath();
                    return;
                }

                if (CurrentPath.CurrentSelectedAvatar != null || CurrentPath.CurrentSelectedAuthor != null)
                {
                    GenerateCategoryList();
                    PathTextBox.Text = GeneratePath();
                    return;
                }

                ResetAvatarList(true);
                PathTextBox.Text = GeneratePath();
                return;
            }

            SearchBox.Text = "";
            SearchResultLabel.Text = "";
            _isSearching = false;

            if (CurrentPath.IsEmpty())
            {
                //�G���[�����Đ�
                SystemSounds.Hand.Play();
                return;
            }

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

            if (_authorMode)
            {
                if (CurrentPath.CurrentSelectedCategory != ItemType.Unknown)
                {
                    CurrentPath.CurrentSelectedCategory = ItemType.Unknown;
                    CurrentPath.CurrentSelectedCustomCategory = string.Empty;
                    GenerateCategoryList();
                    PathTextBox.Text = GeneratePath();
                    return;
                }
            }
            else if (!_categoryMode)
            {
                if (CurrentPath.CurrentSelectedCategory != ItemType.Unknown)
                {
                    CurrentPath.CurrentSelectedCategory = ItemType.Unknown;
                    CurrentPath.CurrentSelectedCustomCategory = string.Empty;
                    GenerateCategoryList();
                    PathTextBox.Text = GeneratePath();
                    return;
                }

                if (CurrentPath.CurrentSelectedAvatar == "*")
                {
                    CurrentPath.CurrentSelectedAvatar = null;
                    CurrentPath.CurrentSelectedAvatarPath = null;
                    ResetAvatarList(true);
                    PathTextBox.Text = GeneratePath();
                    return;
                }
            }

            ResetAvatarList(true);
            PathTextBox.Text = GeneratePath();
        }

        // Search Box
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                SearchItems();
            }
        }

        private void SearchItems()
        {
            if (string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchResultLabel.Text = "";
                PathTextBox.Text = GeneratePath();
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

            _isSearching = true;
            SearchFilter searchFilter = Helper.GetSearchFilter(SearchBox.Text);

            if (_openingWindow is Window.ItemFolderCategoryList or Window.ItemFolderItemsList)
            {
                GenerateFilteredFolderItems(searchFilter);
            }
            else
            {
                GenerateFilteredItem(searchFilter);
            }

            string[] pathTextArr = Array.Empty<string>();
            if (searchFilter.Author.Length != 0)
            {
                pathTextArr = pathTextArr.Append(Helper.Translate("���", CurrentLanguage) + ": " +
                                                 string.Join(", ", searchFilter.Author))
                    .ToArray();
            }

            if (searchFilter.Title.Length != 0)
            {
                pathTextArr = pathTextArr.Append(Helper.Translate("�^�C�g��", CurrentLanguage) + ": " +
                                                 string.Join(", ", searchFilter.Title))
                    .ToArray();
            }

            if (searchFilter.BoothId.Length != 0)
            {
                pathTextArr = pathTextArr.Append("BoothID: " + string.Join(", ", searchFilter.BoothId)).ToArray();
            }

            if (searchFilter.Avatar.Length != 0)
            {
                pathTextArr = pathTextArr.Append(Helper.Translate("�A�o�^�[", CurrentLanguage) + ": " +
                                                 string.Join(", ", searchFilter.Avatar))
                    .ToArray();
            }

            if (searchFilter.Category.Length != 0)
            {
                pathTextArr = pathTextArr.Append(Helper.Translate("�J�e�S��", CurrentLanguage) + ": " +
                                                 string.Join(", ", searchFilter.Category))
                    .ToArray();
            }

            if (searchFilter.ItemMemo.Length != 0)
            {
                pathTextArr = pathTextArr.Append(Helper.Translate("����", CurrentLanguage) + ": " +
                                                 string.Join(", ", searchFilter.ItemMemo))
                    .ToArray();
            }

            pathTextArr = pathTextArr.Append(string.Join(", ", searchFilter.SearchWords)).ToArray();

            PathTextBox.Text = Helper.Translate("������... - ", CurrentLanguage) + string.Join(" / ", pathTextArr);
        }

        // ResetAvatarList
        private void ResetAvatarList(bool startLabelVisible = false)
        {
            if (startLabelVisible)
            {
                _authorMode = false;
                _categoryMode = false;
                CurrentPath = new CurrentPath();
            }

            for (int i = AvatarItemExplorer.Controls.Count - 1; i >= 0; i--)
            {
                if (AvatarItemExplorer.Controls[i].Name != "StartLabel")
                {
                    var control = AvatarItemExplorer.Controls[i];
                    AvatarItemExplorer.Controls.RemoveAt(i);
                    control.Dispose();
                }
                else
                {
                    AvatarItemExplorer.Controls[i].Visible = startLabelVisible;
                }
            }
        }

        // ResetAvatarPage
        private static void ResetAvatarPage(Control page)
        {
            for (int i = page.Controls.Count - 1; i >= 0; i--)
            {
                var control = page.Controls[i];
                page.Controls.RemoveAt(i);
                control.Dispose();
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

            if (!(File.Exists(folderPath) && folderPath.EndsWith(".zip")) && !Directory.Exists(folderPath))
            {
                MessageBox.Show(Helper.Translate("�t�H���_��I�����Ă�������", CurrentLanguage),
                    Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AddItem addItem = new(this, CurrentPath.CurrentSelectedCategory, CurrentPath.CurrentSelectedCustomCategory, false, null, folderPath);
            EventHandler itemAdded = (_, _) =>
            {
                RefleshWindow();
                Helper.SaveItemsData(Items);
                Enabled = true;
            };

            addItem.ItemAdded += itemAdded;
            addItem.FormClosed += (_, _) => addItem.ItemAdded -= itemAdded;
            addItem.Show();
            Enabled = false;
        }

        private void AvatarPage_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[]? dragFilePathArr = (string[]?)e.Data.GetData(DataFormats.FileDrop, false);
            if (dragFilePathArr == null) return;
            var folderPath = dragFilePathArr[0];

            if (!(File.Exists(folderPath) && folderPath.EndsWith(".zip")) && !Directory.Exists(folderPath))
            {
                MessageBox.Show(Helper.Translate("�t�H���_��I�����Ă�������", CurrentLanguage),
                    Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AddItem addItem = new(this, ItemType.Avatar, null, false, null, folderPath);
            addItem.ItemAdded += (_, _) =>
            {
                RefleshWindow();
                Helper.SaveItemsData(Items);
                Enabled = true;
            };
            addItem.Show();
            Enabled = false;
        }

        // Export to CSV
        private void ExportButton_Click(object sender, EventArgs e)
        {
            try
            {
                ExportButton.Enabled = false;
                if (!Directory.Exists("./Output"))
                {
                    Directory.CreateDirectory("./Output");
                }

                var currentTimeStr = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                var fileName = currentTimeStr + ".csv";

                var index = 1;
                while (File.Exists("./Output/" + fileName))
                {
                    if (index > 60) throw new Exception("Too many exports.");
                    fileName = currentTimeStr + $"_{index}.csv";
                    index++;
                }

                using var sw = new StreamWriter("./Output/" + fileName, false, Encoding.UTF8);
                sw.WriteLine("Title,AuthorName,AuthorImageFilePath,ImagePath,Type,Memo,SupportedAvatar,BoothId,ItemPath");
                foreach (var item in Items)
                {
                    string[] avatarNames = Array.Empty<string>();
                    foreach (var avatar in item.SupportedAvatar)
                    {
                        var avatarName = Helper.GetAvatarName(Items, avatar);
                        if (avatarName == null) continue;
                        avatarNames = avatarNames.Append(avatarName).ToArray();
                    }

                    var itemTitle = Helper.EscapeCsv(item.Title);
                    var authorName = Helper.EscapeCsv(item.AuthorName);
                    var authorImageFilePath = Helper.EscapeCsv(item.AuthorImageFilePath);
                    var imagePath = Helper.EscapeCsv(item.ImagePath);
                    var type = Helper.EscapeCsv(Helper.GetCategoryName(item.Type, CurrentLanguage, item.CustomCategory));
                    var memo = Helper.EscapeCsv(item.ItemMemo);
                    var supportedAvatar = Helper.EscapeCsv(string.Join(";", avatarNames));
                    var boothId = Helper.EscapeCsv(item.BoothId.ToString());
                    var itemPath = Helper.EscapeCsv(item.ItemPath);
                    
                    sw.WriteLine($"{itemTitle},{authorName},{authorImageFilePath},{imagePath},{type},{memo},{supportedAvatar},{boothId},{itemPath}");
                }

                MessageBox.Show(Helper.Translate("Output�t�H���_�ɃG�N�X�|�[�g���������܂����I\n�t�@�C����: ", CurrentLanguage) + fileName,
                    Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                ExportButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Helper.Translate("�G�N�X�|�[�g�Ɏ��s���܂���", CurrentLanguage),
                    Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                Helper.ErrorLogger("�G�N�X�|�[�g�Ɏ��s���܂����B", ex);
                ExportButton.Enabled = true;
            }
        }

        // Make Backup
        private void MakeBackupButton_Click(object sender, EventArgs e)
        {
            try
            {
                MakeBackupButton.Enabled = false;
                if (!Directory.Exists("./Backup"))
                {
                    Directory.CreateDirectory("./Backup");
                }

                var currentTimeStr = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                var fileName = currentTimeStr + ".zip";

                var index = 1;
                while (File.Exists("./Backup/" + fileName))
                {
                    if (index > 60) throw new Exception("Too many backups");
                    fileName = currentTimeStr + $"_{index}.zip";
                    index++;
                }

                ZipFile.CreateFromDirectory("./Datas", "./Backup/" + fileName);

                MessageBox.Show(
                    Helper.Translate(
                        "Backup�t�H���_�Ƀo�b�N�A�b�v���������܂����I\n\n�����������ꍇ�́A\"�f�[�^��ǂݍ���\"�{�^���Ō��ݍ쐬���ꂽ�t�@�C����W�J�������̂�I�����Ă��������B\n\n�t�@�C����: ",
                        CurrentLanguage) + fileName, Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                MakeBackupButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Helper.Translate("�o�b�N�A�b�v�Ɏ��s���܂���", CurrentLanguage),
                    Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                Helper.ErrorLogger("�o�b�N�A�b�v�Ɏ��s���܂����B", ex);
                MakeBackupButton.Enabled = true;
            }
        }

        // Change Language
        private void LanguageBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentLanguage = LanguageBox.SelectedIndex switch
            {
                0 => "ja-JP",
                1 => "ko-KR",
                2 => "en-US",
                _ => CurrentLanguage
            };

            var newFont = _fontFamilies.TryGetValue(CurrentLanguage, out var family) ? family : _fontFamilies["ja-JP"];
            GuiFont = newFont;

            foreach (Control control in Controls)
            {
                if (control.Name == "LanguageBox") continue;
                if (string.IsNullOrEmpty(control.Text)) continue;
                _controlNames.TryAdd(control.Name, control.Text);
                control.Text = Helper.Translate(_controlNames[control.Name], CurrentLanguage);
                ChangeControlFont(control);
            }

            string[] sortingItems = { "�^�C�g��", "���" };
            var selected = SortingBox.SelectedIndex;
            SortingBox.Items.Clear();
            SortingBox.Items.AddRange(sortingItems.Select(item => Helper.Translate(item, CurrentLanguage)).ToArray());
            SortingBox.SelectedIndex = selected;

            foreach (Control control in AvatarSearchFilterList.Controls)
            {
                if (string.IsNullOrEmpty(control.Text)) continue;
                _controlNames.TryAdd(control.Name, control.Text);
                control.Text = Helper.Translate(_controlNames[control.Name], CurrentLanguage);
                ChangeControlFont(control);
            }

            foreach (Control control in ExplorerList.Controls)
            {
                if (string.IsNullOrEmpty(control.Text)) continue;
                _controlNames.TryAdd(control.Name, control.Text);
                control.Text = Helper.Translate(_controlNames[control.Name], CurrentLanguage);
                ChangeControlFont(control);
            }

            foreach (Control control in AvatarItemExplorer.Controls)
            {
                if (string.IsNullOrEmpty(control.Text)) continue;
                _controlNames.TryAdd(control.Name, control.Text);
                control.Text = Helper.Translate(_controlNames[control.Name], CurrentLanguage);
                ChangeControlFont(control);
            }

            PathTextBox.Text = GeneratePath();
            RefleshWindow();
        }

        private void SortingBox_SelectedIndexChanged(object sender, EventArgs e) => RefleshWindow();

        private void ChangeControlFont(Control control)
        {
            if (GuiFont == null) return;
            var previousFont = control.Font;
            var familyName = previousFont.FontFamily.Name;
            if (familyName == "Yu Gothic UI") return;
            var previousSize = control.Font.Size;
            if (previousSize is < 0 or >= float.MaxValue) return;
            control.Font = new Font(GuiFont, previousSize);
        }

        private void RefleshWindow(bool reloadLeft = true)
        {
            if (_isSearching)
            {
                SearchItems();
                GenerateAvatarList();
                GenerateAuthorList();
                GenerateCategoryListLeft();
                return;
            }

            switch (_openingWindow)
            {
                case Window.ItemList:
                    GenerateItems();
                    break;
                case Window.ItemCategoryList:
                    GenerateCategoryList();
                    break;
                case Window.ItemFolderCategoryList:
                    GenerateItemCategoryList();
                    break;
                case Window.ItemFolderItemsList:
                    GenerateItemFiles();
                    break;
                case Window.Nothing:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!reloadLeft) return;
            GenerateAvatarList();
            GenerateAuthorList();
            GenerateCategoryListLeft();
        }

        private void ManageCommonAvatarButton_Click(object sender, EventArgs e)
        {
            ManageCommonAvatars manageCommonAvatar = new(this);
            manageCommonAvatar.ShowDialog();
            RefleshWindow();
            PathTextBox.Text = GeneratePath();
            Helper.SaveCommonAvatarData(CommonAvatars);
        }

        // Load Data Button
        private void LoadData_Click(object sender, EventArgs e) => LoadDataFromFolder();

        // Load Data From Folder
        private void LoadDataFromFolder()
        {
            //�����o�b�N�A�b�v�t�H���_���畜�����邩����
            var result = MessageBox.Show(Helper.Translate("�����o�b�N�A�b�v�t�H���_���畜�����܂����H", CurrentLanguage),
                Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                //�o�b�N�A�b�v��̃t�H���_
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var backupPath = Path.Combine(appDataPath, "Avatar Explorer", "Backup");

                //�o�b�N�A�b�v�t�H���_�����݂��Ȃ��ꍇ
                if (!Directory.Exists(backupPath))
                {
                    MessageBox.Show(Helper.Translate("�o�b�N�A�b�v�t�H���_��������܂���ł����B", CurrentLanguage),
                        Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                //�ŏ��̃t�H���_
                var firstFolder = Directory.GetDirectories(backupPath).MaxBy(d => new DirectoryInfo(d).CreationTime) ??
                                  backupPath;

                FolderBrowserDialog fbd = new()
                {
                    UseDescriptionForTitle = true,
                    Description = Helper.Translate("�������鎞�Ԃ̃o�b�N�A�b�v�t�H���_��I�����Ă�������", CurrentLanguage),
                    ShowNewFolderButton = false,
                    SelectedPath = firstFolder
                };

                if (fbd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    var filePath = fbd.SelectedPath + "/ItemsData.json";
                    if (!File.Exists(filePath))
                    {
                        MessageBox.Show(Helper.Translate("�A�C�e���t�@�C����������܂���ł����B", CurrentLanguage),
                            Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        Items = Helper.LoadItemsData(filePath);
                        Items = Helper.FixSupportedAvatarPath(Items);
                        Helper.SaveItemsData(Items);
                    }

                    var filePath2 = fbd.SelectedPath + "/CommonAvatar.json";
                    if (!File.Exists(filePath2))
                    {
                        MessageBox.Show(Helper.Translate("���ʑf�̃t�@�C����������܂���ł����B", CurrentLanguage),
                            Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        CommonAvatars = Helper.LoadCommonAvatarData(filePath2);
                        Helper.SaveCommonAvatarData(CommonAvatars);
                    }

                    var customCategoryPath = fbd.SelectedPath + "/CustomCategory.txt";
                    if (!File.Exists(customCategoryPath))
                    {
                        MessageBox.Show(Helper.Translate("�J�X�^���J�e�S���[�t�@�C����������܂���ł����B", CurrentLanguage),
                            Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        CustomCategories = Helper.LoadCustomCategoriesData(customCategoryPath);
                        Helper.SaveCustomCategoriesData(CustomCategories);
                    }

                    MessageBox.Show(Helper.Translate("�������������܂����B", CurrentLanguage),
                        Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Helper.ErrorLogger("�f�[�^�̓ǂݍ��݂Ɏ��s���܂����B", ex);
                    MessageBox.Show(Helper.Translate("�f�[�^�̓ǂݍ��݂Ɏ��s���܂����B�ڍׂ�ErrorLog.txt���������������B", CurrentLanguage),
                        Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                FolderBrowserDialog fbd = new()
                {
                    UseDescriptionForTitle = true,
                    Description = Helper.Translate("�ȑO�̃o�[�W������Datas�t�H���_�A�������͓W�J�����o�b�N�A�b�v�t�H���_��I�����Ă�������", CurrentLanguage),
                    ShowNewFolderButton = false
                };
                if (fbd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    if (Directory.Exists(Path.Combine(fbd.SelectedPath, "Datas")) &&
                        File.Exists(Path.Combine(fbd.SelectedPath, "Datas", "ItemsData.json")) &&
                        !File.Exists(Path.Combine(fbd.SelectedPath, "ItemsData.json")))
                    {
                        fbd.SelectedPath += "/Datas";
                    }

                    var filePath = fbd.SelectedPath + "/ItemsData.json";
                    if (!File.Exists(filePath))
                    {
                        MessageBox.Show(Helper.Translate("�A�C�e���t�@�C����������܂���ł����B", CurrentLanguage),
                            Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        Items = Helper.LoadItemsData(filePath);
                        Items = Helper.FixSupportedAvatarPath(Items);
                        Helper.SaveItemsData(Items);
                    }

                    var filePath2 = fbd.SelectedPath + "/CommonAvatar.json";
                    if (!File.Exists(filePath2))
                    {
                        MessageBox.Show(Helper.Translate("���ʑf�̃t�@�C����������܂���ł����B", CurrentLanguage),
                            Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        CommonAvatars = Helper.LoadCommonAvatarData(filePath2);
                        Helper.SaveCommonAvatarData(CommonAvatars);
                    }

                    var customCategoryPath = fbd.SelectedPath + "/CustomCategory.txt";
                    if (!File.Exists(customCategoryPath))
                    {
                        MessageBox.Show(Helper.Translate("�J�X�^���J�e�S���[�t�@�C����������܂���ł����B", CurrentLanguage),
                            Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        CustomCategories = Helper.LoadCustomCategoriesData(customCategoryPath);
                        Helper.SaveCustomCategoriesData(CustomCategories);
                    }

                    var result2 = MessageBox.Show(
                        Helper.Translate("Thumbnail�t�H���_�AAuthorImage�t�H���_�AItems�t�H���_���R�s�[���܂����H", CurrentLanguage),
                        Helper.Translate("�m�F", CurrentLanguage), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result2 != DialogResult.Yes)
                    {
                        SearchBox.Text = "";
                        SearchResultLabel.Text = "";
                        _isSearching = false;
                        GenerateAvatarList();
                        GenerateAuthorList();
                        GenerateCategoryListLeft();
                        ResetAvatarList(true);
                        PathTextBox.Text = GeneratePath();
                        MessageBox.Show(Helper.Translate("�R�s�[���������܂����B", CurrentLanguage),
                            Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var thumbnailPath = fbd.SelectedPath + "/Thumbnail";
                    var authorImagePath = fbd.SelectedPath + "/AuthorImage";
                    var itemsPath = fbd.SelectedPath + "/Items";

                    var thumbnailResult = true;
                    var authorImageResult = true;
                    var itemsResult = true;
                    if (Directory.Exists(thumbnailPath))
                    {
                        Directory.CreateDirectory("./Datas/Thumbnail");
                        foreach (var file in Directory.GetFiles(thumbnailPath))
                        {
                            try
                            {
                                File.Copy(file, "./Datas/Thumbnail/" + Path.GetFileName(file), true);
                            }
                            catch (Exception ex)
                            {
                                Helper.ErrorLogger("�T���l�C���̃R�s�[�Ɏ��s���܂����B", ex);
                                thumbnailResult = false;
                            }
                        }
                    }

                    if (Directory.Exists(authorImagePath))
                    {
                        Directory.CreateDirectory("./Datas/AuthorImage");
                        foreach (var file in Directory.GetFiles(authorImagePath))
                        {
                            try
                            {
                                File.Copy(file, "./Datas/AuthorImage/" + Path.GetFileName(file), true);
                            }
                            catch (Exception ex)
                            {
                                Helper.ErrorLogger("��҉摜�̃R�s�[�Ɏ��s���܂����B", ex);
                                authorImageResult = false;
                            }
                        }
                    }

                    if (Directory.Exists(itemsPath))
                    {
                        try
                        {
                            Helper.CopyDirectory(itemsPath, "./Datas/Items");
                        }
                        catch (Exception ex)
                        {
                            Helper.ErrorLogger("Items�̃R�s�[�Ɏ��s���܂����B", ex);
                            itemsResult = false;
                        }
                    }

                    MessageBox.Show(Helper.Translate("�R�s�[���������܂����B", CurrentLanguage) + "\n\n" + Helper.Translate("�R�s�[���s�ꗗ: ", CurrentLanguage) +
                                    (thumbnailResult ? "" : "\n" + Helper.Translate("�T���l�C���̃R�s�[�Ɉꕔ���s���Ă��܂��B", CurrentLanguage)) +
                                    (authorImageResult ? "" : "\n" + Helper.Translate("��҉摜�̃R�s�[�Ɉꕔ���s���Ă��܂��B", CurrentLanguage)) +
                                    (itemsResult ? "" : "\n" + Helper.Translate("Items�̃R�s�[�Ɉꕔ���s���Ă��܂��B", CurrentLanguage)),
                        Helper.Translate("����", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Helper.ErrorLogger("�f�[�^�̓ǂݍ��݂Ɏ��s���܂����B", ex);
                    MessageBox.Show(Helper.Translate("�f�[�^�̓ǂݍ��݂Ɏ��s���܂����B�ڍׂ�ErrorLog.txt���������������B", CurrentLanguage),
                        Helper.Translate("�G���[", CurrentLanguage), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            SearchBox.Text = "";
            SearchResultLabel.Text = "";
            _isSearching = false;
            GenerateAvatarList();
            GenerateAuthorList();
            GenerateCategoryListLeft();
            ResetAvatarList(true);
            PathTextBox.Text = GeneratePath();
        }

        // Resize Form
        private void Main_Resize(object sender, EventArgs e) => ResizeControl();

        private void ResizeControl()
        {
            if (!_initialized) return;
            var widthRatio = (float)ClientSize.Width / _initialFormSize.Width;
            var heightRatio = (float)ClientSize.Height / _initialFormSize.Height;

            foreach (Control control in Controls)
            {
                // �T�C�Y�̃X�P�[�����O
                if (!_defaultControlSize.TryGetValue(control.Name, out var defaultSize))
                {
                    defaultSize = new SizeF(control.Size.Width, control.Size.Height);
                    _defaultControlSize.Add(control.Name, defaultSize);
                }

                var newWidth = (int)(defaultSize.Width * widthRatio);
                var newHeight = (int)(defaultSize.Height * heightRatio);

                // �T�C�Y���N���C�A���g�̈�𒴂��Ȃ��悤�ɐ���
                newWidth = Math.Min(newWidth, ClientSize.Width);
                newHeight = Math.Min(newHeight, ClientSize.Height);

                control.Size = new Size(newWidth, newHeight);

                // �ʒu�̃X�P�[�����O
                if (!_defaultControlLocation.TryGetValue(control.Name, out var defaultLocation))
                {
                    defaultLocation = new PointF(control.Location.X, control.Location.Y);
                    _defaultControlLocation.Add(control.Name, defaultLocation);
                }

                var newX = (int)(defaultLocation.X * widthRatio);
                var newY = (int)(defaultLocation.Y * heightRatio);

                // �ʒu���N���C�A���g�̈�𒴂��Ȃ��悤�ɐ���
                newX = Math.Max(0, Math.Min(newX, ClientSize.Width - control.Width));
                newY = Math.Max(0, Math.Min(newY, ClientSize.Height - control.Height));

                control.Location = new Point(newX, newY);

                switch (control)
                {
                    // ���x���A�e�L�X�g�{�b�N�X�̃t�H���g�T�C�Y�̃X�P�[�����O
                    case Label { Name: "SearchResultLabel" } label:
                        {
                            if (!_defaultFontSize.TryGetValue(label.Name, out var defaultFontSize))
                            {
                                defaultFontSize = label.Font.Size;
                                _defaultFontSize.Add(label.Name, defaultFontSize);
                            }

                            var scaleRatio = Math.Min(widthRatio, heightRatio);
                            var newFontSize = defaultFontSize * scaleRatio;

                            // �������Ȃ�ꍇ�̂݃t�H���g�T�C�Y��ύX
                            if (!(newFontSize < defaultFontSize)) continue;
                            newFontSize = Math.Max(newFontSize, MinFontSize);
                            if (newFontSize is < 0 or >= float.MaxValue) continue;
                            label.Font = new Font(label.Font.FontFamily, newFontSize, label.Font.Style);
                            break;
                        }
                    // SearchResultLabel�ȊO
                    case Label label:
                        {
                            if (!_defaultFontSize.TryGetValue(label.Name, out var defaultFontSize))
                            {
                                defaultFontSize = label.Font.Size;
                                _defaultFontSize.Add(label.Name, defaultFontSize);
                            }

                            var scaleRatio = Math.Min(widthRatio, heightRatio);
                            var newFontSize = defaultFontSize * scaleRatio;
                            newFontSize = Math.Max(newFontSize, MinFontSize);
                            if (newFontSize is < 0 or >= float.MaxValue) continue;
                            label.Font = new Font(label.Font.FontFamily, newFontSize, label.Font.Style);
                            break;
                        }
                    case TextBox:
                        {
                            if (!_defaultFontSize.TryGetValue(control.Name, out var defaultFontSize))
                            {
                                defaultFontSize = control.Font.Size;
                                _defaultFontSize.Add(control.Name, defaultFontSize);
                            }

                            var scaleRatio = Math.Min(widthRatio, heightRatio);
                            var newFontSize = defaultFontSize * scaleRatio;
                            newFontSize = Math.Max(newFontSize, MinFontSize);
                            if (newFontSize is < 0 or >= float.MaxValue) continue;
                            control.Font = new Font(control.Font.FontFamily, newFontSize, control.Font.Style);
                            break;
                        }
                }
            }

            for (int i = AvatarItemExplorer.Controls.Count - 1; i >= 0; i--)
            {
                if (AvatarItemExplorer.Controls[i].Name != "StartLabel") continue;
                var control = AvatarItemExplorer.Controls[i];
                control.Location = control.Location with
                {
                    X = (AvatarItemExplorer.Width - control.Width) / 2,
                    Y = (AvatarItemExplorer.Height - control.Height) / 2
                };
            }

            ScaleItemButtons();
        }

        private void ScaleItemButtons()
        {
            const int avatarItemExplorerBaseWidth = 874;
            const int avatarItemListBaseWidth = 303;

            var avatarItemExplorerWidth = avatarItemExplorerBaseWidth + GetItemExplorerListWidth();
            var avatarItemListWidth = avatarItemListBaseWidth + GetAvatarListWidth();

            foreach (Control control in AvatarItemExplorer.Controls)
            {
                if (control is Button button)
                {
                    button.Size = button.Size with { Width = avatarItemExplorerWidth };
                }
            }

            var controls = new Control[]
            {
                AvatarPage,
                AvatarAuthorPage,
                CategoryPage
            };

            foreach (var control in controls)
            {
                foreach (Control control1 in control.Controls)
                {
                    if (control1 is Button button)
                    {
                        button.Size = button.Size with { Width = avatarItemListWidth };
                    }
                }
            }
        }

        // Backup
        private void AutoBackup()
        {
            BackupFile();
            Timer timer = new()
            {
                Interval = BackupInterval
            };

            timer.Tick += (_, _) => BackupFile();
            timer.Start();
        }

        private void BackupTimeTitle()
        {
            Timer timer = new()
            {
                Interval = 1000
            };

            timer.Tick += (_, _) =>
            {
                if (_lastBackupTime == DateTime.MinValue)
                {
                    if (_lastBackupError)
                        Text = CurrentVersionFormText + " - " + Helper.Translate("�o�b�N�A�b�v�G���[", CurrentLanguage);
                    return;
                }

                var timeSpan = DateTime.Now - _lastBackupTime;
                var minutes = timeSpan.Minutes;
                Text = CurrentVersionFormText +
                       $" - {Helper.Translate("�ŏI�����o�b�N�A�b�v: ", CurrentLanguage) + minutes + Helper.Translate("���O", CurrentLanguage)}";

                if (_lastBackupError) Text += " - " + Helper.Translate("�o�b�N�A�b�v�G���[", CurrentLanguage);
            };
            timer.Start();
        }

        private void BackupFile()
        {
            try
            {
                var backupFilesArray = new[]
                {
                    "./Datas/ItemsData.json",
                    "./Datas/CommonAvatar.json",
                    "./Datas/CustomCategory.txt"
                };

                Helper.Backup(backupFilesArray);
                _lastBackupError = false;
                _lastBackupTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                _lastBackupError = true;
                Helper.ErrorLogger("�����o�b�N�A�b�v�Ɏ��s���܂����B", ex);
            }
        }

        // Form Closing
        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (!Directory.Exists("./Datas/Temp")) return;
                Directory.Delete("./Datas/Temp", true);
            }
            catch (Exception ex)
            {
                Helper.ErrorLogger("�ꎞ�t�H���_�̍폜�Ɏ��s���܂����B", ex);
            }
        }
    }
}