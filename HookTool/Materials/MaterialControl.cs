using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TrRebootTools.Shared.Cdc;
using TrRebootTools.Shared.Controls.VirtualTreeView;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool.Materials
{
    internal partial class MaterialControl : UserControl
    {
        private record struct ShaderKey(Material.Pass Pass, ShaderType ShaderType);

        private GameProcess _process;
        private CdcGame _game;
        private bool _ingame;

        private readonly Dictionary<FileInfo, Material> _materials = new();
        private readonly Dictionary<ShaderKey, BindingList<MaterialConstant>> _constants = new();
        
        private Material _currentMaterial;
        private Material.Pass _currentPass;
        private ShaderType _currentShaderType = ShaderType.Pixel;
        private BindingList<MaterialConstant> _currentConstants;

        public MaterialControl()
        {
            InitializeComponent();
            _lvPasses.Header.Columns.Add(new VirtualTreeColumn { Name = "" });
            _lvPasses.OnGetNodeCellText += OnGetPassNodeText;
            _lblMaterial.Font = new Font(_lblMaterial.Font, FontStyle.Bold);

            _tvMaterials_SelectionChanged(_tvMaterials, EventArgs.Empty);
            UpdateActionButtons();
        }

        public void Init(GameProcess process, CdcGame game)
        {
            _process = process;
            _game = game;

            _process.Events.GameEntered += HandleGameEntered;
            _process.Exited += HandleProcessExited;
        }

        public void SetModFolder(string folderPath)
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

                    _constants[new ShaderKey(pass, shaderType)] = new BindingList<MaterialConstant>(
                        constants.Select(c => new MaterialConstant(c)).ToList()
                    );
                }
            }
            return true;
        }

        private void HandleGameEntered()
        {
            _ingame = true;
        }

        private void _tvMaterials_SelectionChanged(object sender, EventArgs e)
        {
            FileInfo file = _tvMaterials.ActiveFile;
            Material material = file != null ? _materials.GetOrDefault(file) : null;
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

        private void _lvPasses_OnSelectionChanged(object sender, EventArgs e)
        {
            VirtualTreeNode node = _lvPasses.ActiveNode;
            CurrentPass = node != null ? _lvPasses.GetNodeData<Material.Pass>(node) : null;
        }

        private void OnGetPassNodeText(VirtualTreeView tree, VirtualTreeNode node, int column, out string cellText)
        {
            Material.Pass pass = tree.GetNodeData<Material.Pass>(node);
            cellText = $"Pass {pass.Index}";
        }

        private void _radPixelShader_CheckedChanged(object sender, EventArgs e)
        {
            CurrentShaderType = _radPixelShader.Checked ? ShaderType.Pixel : ShaderType.Vertex;
        }

        private void _btnSave_Click(object sender, EventArgs e)
        {
            FileInfo file = _tvMaterials.ActiveFile;
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
                MessageBox.Show(ex.Message, "Failed to save material", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (_currentConstants == null)
                return;

            foreach (MaterialConstant constant in _currentConstants)
            {
                constant.MarkClean();
            }
        }

        public event EventHandler SavingMaterial;

        private void _btnRevert_Click(object sender, EventArgs e)
        {
            if (_currentConstants == null)
                return;

            _currentConstants.RaiseListChangedEvents = false;
            foreach (MaterialConstant constant in _currentConstants)
            {
                constant.Reset();
            }
            _currentConstants.RaiseListChangedEvents = true;
            _currentConstants.ResetBindings();
        }

        private Material CurrentMaterial
        {
            get => _currentMaterial;
            set
            {
                if (value == _currentMaterial)
                    return;

                _currentMaterial = value;
                if (_currentMaterial == null)
                {
                    _lvPasses.Clear();
                    CurrentPass = null;
                    return;
                }

                _lvPasses.BeginUpdate();
                _lvPasses.Clear();
                int? passIdx = _currentPass?.Index;
                foreach (Material.Pass pass in _currentMaterial.Passes)
                {
                    VirtualTreeNode passNode = _lvPasses.InsertNode(null, NodeAttachMode.amAddChildLast, pass);
                    if (pass.Index == passIdx)
                        _lvPasses.ActiveNode = passNode;
                }
                _lvPasses.EndUpdate();
                _lvPasses_OnSelectionChanged(_lvPasses, EventArgs.Empty);
            }
        }

        private Material.Pass CurrentPass
        {
            get => _currentPass;
            set
            {
                if (value == _currentPass)
                    return;

                _currentPass = value;
                _pnlShaderTypes.Enabled = _currentPass != null;
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
                _currentConstants.ListChanged -= HandleConstantChanged;

            _currentConstants = _currentPass != null ? _constants.GetOrDefault(new ShaderKey(_currentPass, _currentShaderType)) : null;

            if (_currentConstants != null)
                _currentConstants.ListChanged += HandleConstantChanged;

            _constantsControl.DataSource = _currentConstants;
            UpdateActionButtons();
        }

        private void HandleConstantChanged(object sender, ListChangedEventArgs e)
        {
            UpdateActionButtons();
            bool propertyChanged = e.ListChangedType == ListChangedType.ItemChanged &&
                                   e.PropertyDescriptor?.Name is nameof(MaterialConstant.X) or
                                                                 nameof(MaterialConstant.Y) or
                                                                 nameof(MaterialConstant.Z) or
                                                                 nameof(MaterialConstant.W);
            if (propertyChanged || e.ListChangedType == ListChangedType.Reset)
                SendConstantsToGame(_currentMaterial, _currentPass, _currentShaderType);
        }

        private void UpdateActionButtons()
        {
            _pnlActions.Enabled = _currentConstants != null && _currentConstants.Any(c => c.Dirty);
        }

        private void SendConstantsToGame(Material material, Material.Pass pass, ShaderType shaderType)
        {
            if (material == null || pass == null || !_ingame)
                return;

            _process.Commands.SetMaterialConstants(material.Id, pass.Index, shaderType, pass.GetConstants(shaderType));
        }

        private void HandleProcessExited()
        {
            _ingame = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (_process?.Events != null)
                _process.Events.GameEntered -= HandleGameEntered;

            if (_process != null)
                _process.Exited -= HandleProcessExited;

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
