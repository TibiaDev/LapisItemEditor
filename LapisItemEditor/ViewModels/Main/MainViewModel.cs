using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Converters;
using DynamicData;
using LapisItemEditor.ViewModels.ItemProperties;
using ReactiveUI;
using System.Text.Json;

using static LapisItemEditor.ViewModels.ItemListViewModel;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace LapisItemEditor.ViewModels.Main
{
    public class MainViewModel : ReactiveObject, IRoutableViewModel
    {
        // Reference to IScreen that owns the routable view model.
        public IScreen HostScreen { get; }

        // Unique identifier for the routable view model.
        public string UrlPathSegment { get; } = "MainViewModel";

        ItemListViewModel Items { get; } = new();
        ItemPropertiesViewModel ItemProperties { get; } = new();

        private MainWindowViewModel mainModel => (MainWindowViewModel)HostScreen;

        public MainViewModel(IScreen screen)
        {
            HostScreen = screen;

            Items.ItemTypeSelected.Subscribe(itemModel =>
            {
                ItemProperties.Appearance = itemModel.Appearance;
            });

            CreateMissingItems = ReactiveCommand.Create(() =>
            {
                if (Backend.Backend.GameData == null)
                {
                    throw new InvalidDataException("No GameData loaded!");
                }

                int oldLastServerId = (int)Backend.Backend.GameData.LastItemTypeServerId;

                Backend.Backend.GameData.CreateMissingItems();

                int newLastServerId = (int)Backend.Backend.GameData.LastItemTypeServerId;

                AddItems(oldLastServerId + 1, newLastServerId + 1);
            });

            WriteItemsOtb = ReactiveCommand.Create(async () =>
            {
                if (Backend.Backend.GameData?.OtbData?.Version != null)
                {
                    if (InputClientVersion != null)
                    {
                        Backend.Backend.GameData.OtbData.Version.MinorVersion = OtbMinorVersion;
                        Backend.Backend.GameData.OtbData.Version.MajorVersion = OtbMajorVersion;
                        Backend.Backend.GameData.Version.Version = (Backend.ClientVersion)InputClientVersion.Version;

                        // Hard-coded version
                        // Backend.Backend.GameData.OtbData.Version.MinorVersion = 60;
                        // Backend.Backend.GameData.Version.Version = Backend.ClientVersion.V12_81;

                        var otbWritePath = await ShowSaveItemsOtbDialog();

                        if (otbWritePath != null && Directory.Exists(Path.GetDirectoryName(otbWritePath)))
                        {
                            // Trace.WriteLine($"Writing items.otb with client version {InputClientVersion.Version}, Major OTB version {OtbMajorVersion} and Minor OTB version {OtbMinorVersion}.");
                            try
                            {
                                Backend.Backend.GameData?.WriteOtb(otbWritePath);
                                mainModel.InfoMessage = $"OTB file (Version: {OtbMajorVersion}.{OtbMinorVersion}, Client: {InputClientVersion.Version}) saved as {otbWritePath}.";
                            }
                            catch (Exception e)
                            {
                                Trace.WriteLine(e);
                                mainModel.InfoMessage = "ERROR: when trying to write OTB to {otbWritePath}.";
                            }
                        }
                    }
                    else
                    {
                        Trace.WriteLine($"No client version specified.");
                    }
                }
            });

            WriteClientData = ReactiveCommand.Create(async () =>
            {
                if (Backend.Backend.GameData != null)
                {
                    if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var window = desktop.MainWindow;
                        if (window != null)
                        {
                            var result = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                            {
                                Title = "Select client data folder",
                                AllowMultiple = false,
                            });

                            var selectedFolder = result?[0];

                            if (selectedFolder != null)
                            {
                                Backend.Backend.GameData.WriteClientData(selectedFolder.Path.AbsolutePath);
                                mainModel.InfoMessage = $"Wrote client data to {selectedFolder.Path.AbsolutePath}.";
                            }
                        }
                    }
                }
            });

            ImportItemNames = ReactiveCommand.Create(async () =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var dialog = new OpenFileDialog() { AllowMultiple = false, Title = "Select item names file" };
                    var result = await dialog.ShowAsync(desktop.MainWindow);
                    if (result.Length != 0 && File.Exists(result[0]))
                    {
                        ImportItemNamesFromFile(result[0]);
                    }
                    else
                    {
                        Trace.WriteLine("Invalid file path chosen when trying to import item names.");
                    }
                }
            });

            ExportItemsXml = ReactiveCommand.Create(async () =>
            {
                StringBuilder sb = new StringBuilder("", Items.Items.Count * 50);
                foreach (var item in Items.Items.Items)
                {

                    var row = $"<item id=\"{item.ServerId}\"";
                    if (item.Appearance?.Article != null)
                    {
                        row += $" article=\"{item.Appearance?.Article}\"";
                    }
                    if (item.Name != null)
                    {
                        row += $" name=\"{item.Name}\"";
                    }
                    row += "/>\n";
                    sb.Append(row);
                }

                await File.WriteAllTextAsync("items.xml", sb.ToString());
            });

            SyncOtbWithTibia = ReactiveCommand.Create(async () =>
            {
                var changes = new List<Backend.Appearance.ChangeEntry>();
                var sb = new StringBuilder("");
                foreach (var item in Items.Items.Items)
                {
                    var otbChanges = item.Appearance?.SyncOtbWithTibia();

                    if (otbChanges != null)
                    {
                        changes.Add(otbChanges);
                    }
                }

                string jsonString = JsonSerializer.Serialize(changes);


                var path = Path.GetDirectoryName("./logs/sync-otb-with-tibia.log.json");
                if (path != null)
                {
                    Directory.CreateDirectory(path);
                    await File.WriteAllTextAsync(path, jsonString);
                    Trace.WriteLine(jsonString);
                }
            });
        }

        private void ImportItemNamesFromFile(string path)
        {
            if (Backend.Backend.GameData != null)
            {
                Backend.ItemNameLoader.LoadFromFile(Backend.Backend.GameData, path);

                Items.Items.Edit(model =>
                {
                    model.Clear();
                    model.AddRange(CreateItems());
                });
            }
        }

        public void Load(GameDataConfig config, string? itemsOtbPath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Backend.Backend.GameData = config.CreateGameData();
            if (itemsOtbPath != null)
            {
                Backend.Backend.GameData?.LoadOtb(itemsOtbPath);
            }
            else
            {
                Backend.Backend.GameData?.CreateNewOtb();
            }

            Items.Items.AddRange(CreateItems());

            ClientVersion? defaultClientVersion;

            Backend.OtbData? otbData = Backend.Backend.GameData?.OtbData;
            if (otbData == null)
            {
                throw new InvalidDataException("No OTB data.");
            }

            OtbMinorVersion = otbData.Version.MinorVersion;

            // Add the client version of the loaded OTB to valid client versions if it does not exists.
            int? clientVersion = otbData.ClientVersion;
            if (clientVersion != null)
            {
                Config.instance.AddClientVersion((int)clientVersion);
                defaultClientVersion = Config.instance.ClientVersions.Find(x => x.Version == clientVersion);
            }
            else
            {
                defaultClientVersion = InputClientVersions.Last();
            }

            Config.instance.addMajorOtbVersion(otbData.Version.MajorVersion);


            InputClientVersions = new();
            Config.instance.ClientVersions.ForEach(version => InputClientVersions.Add(version));

            MajorOtbVersions = new();
            Config.instance.MajorOtbVersions.ForEach(version => MajorOtbVersions.Add(version));

            OtbMajorVersion = otbData.Version.MajorVersion;
            InputClientVersion = defaultClientVersion;

            stopwatch.Stop();
            mainModel.InfoMessage = $"Loaded assets in {stopwatch.ElapsedMilliseconds} ms.";
        }

        private void AddItems(int fromServerId, int toServerId)
        {
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            IEnumerable<ItemListViewItemModel> listItems = Enumerable
                .Range(fromServerId, toServerId - fromServerId)
                .Select(serverId => CreateItemModel((uint)serverId))
                .OfType<ItemListViewItemModel>();

            Items.Items.AddRange(listItems);
            stopwatch.Stop();

            Trace.WriteLine($"AddItems finished in {stopwatch.ElapsedMilliseconds} ms.");
        }

        private IEnumerable<ItemListViewItemModel> CreateItems()
        {
            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            IEnumerable<ItemListViewItemModel> listItems = Enumerable
                .Range(100, (int)Backend.Backend.LastItemTypeServerId)
                .Select(serverId => CreateItemModel((uint)serverId))
                .OfType<ItemListViewItemModel>();
            stopwatch.Stop();

            Trace.WriteLine($"Created ItemType list entries in {stopwatch.ElapsedMilliseconds} ms.");

            return listItems;
        }

        private async Task<string?> ShowSaveItemsOtbDialog()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.MainWindow;
                if (window != null)
                {
                    var result = await window.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions()
                    {
                        Title = "Save items.otb",
                        DefaultExtension = "otb",
                        SuggestedFileName = "items",
                    });
                    if (result != null)
                    {
                        return result.Path.AbsolutePath;
                    }
                }
            }

            return null;
        }

        private static ItemListViewItemModel? CreateItemModel(uint serverId)
        {
            var appearance = Backend.Backend.GetItemTypeByServerId(serverId);
            if (appearance != null && appearance.HasSprites)
            {
                uint clientId = appearance.ClientId;


                string name = appearance.Data.Name;

                // Prefer the name from .otb
                if (!string.IsNullOrEmpty(appearance.otbItem?.Name))
                {
                    name = appearance.otbItem.Name;
                }


                return new ItemListViewItemModel()
                {
                    Appearance = appearance,
                    ServerId = serverId,
                    ClientId = clientId,
                    Name = name,
                    Text = $"{serverId} (cid {clientId})"
                };
            }

            return null;
        }

        public ICommand CreateMissingItems { get; }
        public ICommand WriteItemsOtb { get; }
        public ICommand WriteClientData { get; }
        public ICommand ImportItemNames { get; }
        public ICommand ExportItemsXml { get; }
        public ICommand SyncOtbWithTibia { get; }

        private uint otbMinorVersion = 0;
        public uint OtbMinorVersion { get => otbMinorVersion; set => this.RaiseAndSetIfChanged(ref otbMinorVersion, value); }

        private uint otbMajorVersion = 0;
        public uint OtbMajorVersion { get => otbMajorVersion; set => this.RaiseAndSetIfChanged(ref otbMajorVersion, value); }


        private ClientVersion? inputClientVersion;
        public ClientVersion? InputClientVersion { get => inputClientVersion; set => this.RaiseAndSetIfChanged(ref inputClientVersion, value); }

        private ObservableCollection<uint> _majorOtbVersions;
        public ObservableCollection<uint> MajorOtbVersions
        {
            get => _majorOtbVersions;
            set { this.RaiseAndSetIfChanged(ref _majorOtbVersions, value); }
        }


        private ObservableCollection<ClientVersion> _inputClientVersions;
        public ObservableCollection<ClientVersion> InputClientVersions
        {
            get => _inputClientVersions;
            set { this.RaiseAndSetIfChanged(ref _inputClientVersions, value); }
        }
    }

    public class ClientVersionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.Assert(value != null);
            return ((ClientVersion)value).Name;
        }

        public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.Assert(value != null);
            return (ClientVersion)value;
        }
    }
}