using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool.Materials
{
    internal partial class MaterialControl : UserControl
    {
        private record struct ShaderKey(Material.Pass Pass, ShaderType ShaderType);

        private GameProcess? _process;
        private CdcGame _game;
        private bool _ingame;

        private readonly Dictionary<FileInfo, Material> _materials = new();
        private readonly Dictionary<ShaderKey, TrulyObservableCollection<MaterialConstant>> _constants = new();

        private Material? _currentMaterial;
        private Material.Pass? _currentPass;
        private ShaderType _currentShaderType = ShaderType.Pixel;
        private TrulyObservableCollection<MaterialConstant>? _currentConstants;

        public MaterialControl()
        {
            InitializeComponent();
            if (Design.IsDesignMode)
                return;

            OnMaterialSelected(_tvMaterials, EventArgs.Empty);
            UpdateActionButtons();
        }

        public void Init(GameProcess process, CdcGame game)
        {
            _process = process;
            _game = game;

            _process.Events.GameEntered += HandleGameEntered;
            _process.Exited += HandleProcessExited;
        }

        public void SetModFolder(string? folderPath)
        {
            if (_process == null)
                return;

            _materials.Clear();
            _constants.Clear();

            if (string.IsNullOrEmpty(folderPath))
                _tvMaterials.Clear();
            else
                _tvMaterials.Populate(new DirectoryInfo(folderPath), $"*.tr{(int)_game}material", FilterMaterialFile);

            CurrentMaterial = null;

            if (_ingame)
                _process.Commands.ClearStoredMaterialConstants();
        }

        private bool FilterMaterialFile(FileInfo file)
        {
            if (!ResourceNaming.TryGetResourceKey(file.Name, out ResourceKey resourceKey, _game))
                return false;

            Material material;
            try
            {
                using Stream stream = file.OpenRead();
                material = Material.Open(resourceKey.Id, stream, _game);
            }
            catch
            {
                return false;
            }

            _materials[file] = material;
            foreach (Material.Pass pass in material.Passes)
            {
                foreach (ShaderType shaderType in new[] { ShaderType.Pixel, ShaderType.Vertex })
                {
                    Vec4[] constants = pass.GetConstants(shaderType);
                    if (constants == null || constants.Length == 0)
                        continue;

                    _constants[new ShaderKey(pass, shaderType)] = new(
                        constants.Select(c => new MaterialConstant(c))
                    );
                }
            }
            return true;
        }

        private void HandleGameEntered()
        {
            _ingame = true;
        }

        private void OnMaterialSelected(object? sender, EventArgs e)
        {
            FileInfo? file = _tvMaterials.ActiveFile;
            Material? material = file != null ? _materials.GetValueOrDefault(file) : null;
            if (file == null || material == null)
            {
                _lblMaterial.Text = "(No material selected)";
                CurrentMaterial = null;
            }
            else
            {
                _lblMaterial.Text = file.Name;
                CurrentMaterial = material;
            }
        }

        private void OnPassSelected(object? sender, SelectionChangedEventArgs e)
        {
            CurrentPass = _lstPasses.SelectedItem as Material.Pass;
        }

        private void OnShaderTypeChanged(object? sender, RoutedEventArgs e)
        {
            CurrentShaderType = _radPixelShader.IsChecked == true ? ShaderType.Pixel : ShaderType.Vertex;
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            FileInfo? file = _tvMaterials.ActiveFile;
            if (file == null || _currentMaterial == null)
                return;

            SavingMaterial?.Invoke(this, EventArgs.Empty);
            try
            {
                using Stream stream = file.Create();
                _currentMaterial.Write(stream);
            }
            catch (Exception ex)
            {
                await MessageBox.ShowErrorAsync(ex);
                return;
            }
            finally
            {
                SavedMaterial?.Invoke(this, EventArgs.Empty);
            }

            if (_currentConstants == null)
                return;

            foreach (MaterialConstant constant in _currentConstants)
            {
                constant.MarkClean();
            }
        }

        public event EventHandler? SavingMaterial;

        public event EventHandler? SavedMaterial;

        private void OnRevertClick(object? sender, RoutedEventArgs e)
        {
            if (_currentConstants == null)
                return;

            _currentConstants.RaiseItemChangedEvents = false;
            foreach (MaterialConstant constant in _currentConstants)
            {
                constant.Reset();
            }
            _currentConstants.RaiseItemChangedEvents = true;
            UpdateActionButtons();
            SendConstantsToGame(_currentMaterial, _currentPass, _currentShaderType);
        }

        private Material? CurrentMaterial
        {
            get => _currentMaterial;
            set
            {
                if (value == _currentMaterial)
                    return;

                _lstPasses.Items.Clear();

                _currentMaterial = value;
                if (_currentMaterial == null || _currentMaterial.Passes.Count == 0)
                {
                    CurrentPass = null;
                    return;
                }

                int? passIdx = _currentPass?.Index;
                foreach (Material.Pass pass in _currentMaterial.Passes)
                {
                    _lstPasses.Items.Add(pass);
                    if (pass.Index == passIdx)
                        _lstPasses.SelectedIndex = _lstPasses.Items.Count - 1;
                }
            }
        }

        private Material.Pass? CurrentPass
        {
            get => _currentPass;
            set
            {
                if (value == _currentPass)
                    return;

                _currentPass = value;
                _grdShaderTypes.IsEnabled = _currentPass != null;
                SetCurrentShaderConstants();
            }
        }

        private ShaderType CurrentShaderType
        {
            get => _currentShaderType;
            set
            {
                if (value == _currentShaderType)
                    return;

                _currentShaderType = value;
                SetCurrentShaderConstants();
            }
        }

        private void SetCurrentShaderConstants()
        {
            if (_currentConstants != null)
                _currentConstants.ItemChanged -= HandleConstantChanged;

            _currentConstants = _currentPass != null ? _constants.GetValueOrDefault(new ShaderKey(_currentPass, _currentShaderType)) : null;

            if (_currentConstants != null)
                _currentConstants.ItemChanged += HandleConstantChanged;

            _pnlConstants.Children.Clear();
            if (_currentConstants != null)
            {
                for (int i = 0; i < _currentConstants.Count; i++)
                {
                    _pnlConstants.Children.Add(
                        new MaterialConstantControl
                        {
                            Index = i,
                            DataContext = _currentConstants[i]
                        }
                    );
                }
            }
            
            UpdateActionButtons();
        }

        private void HandleConstantChanged(MaterialConstant item, PropertyChangedEventArgs e)
        {
            UpdateActionButtons();
            SendConstantsToGame(_currentMaterial, _currentPass, _currentShaderType);
        }

        private void UpdateActionButtons()
        {
            _pnlActions.IsEnabled = _currentConstants != null && _currentConstants.Any(c => c.Dirty);
        }

        private void SendConstantsToGame(Material? material, Material.Pass? pass, ShaderType shaderType)
        {
            if (material == null || pass == null || !_ingame)
                return;

            _process?.Commands.SetMaterialConstants(material.Id, pass.Index, shaderType, pass.GetConstants(shaderType));
        }

        private void HandleProcessExited()
        {
            _ingame = false;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (_process?.Events != null)
                _process.Events.GameEntered -= HandleGameEntered;

            if (_process != null)
                _process.Exited -= HandleProcessExited;
        }
    }
}
