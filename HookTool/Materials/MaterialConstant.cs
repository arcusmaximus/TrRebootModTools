using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrRebootTools.Shared.Util;

namespace TrRebootTools.HookTool.Materials
{
    internal class MaterialConstant : INotifyPropertyChanged
    {
        private readonly Vec4 _originalValue;
        private readonly Vec4 _value;
        private bool _dirty;

        public MaterialConstant(Vec4 value)
        {
            _originalValue = new();
            _value = value;
            MarkClean();
        }

        public float X
        {
            get => _value.X;
            set
            {
                if (value == _value.X)
                    return;

                _value.X = value;
                RaisePropertyChanged();
                UpdateIsDirty();
            }
        }

        public float Y
        {
            get => _value.Y;
            set
            {
                if (value == _value.Y)
                    return;

                _value.Y = value;
                RaisePropertyChanged();
                UpdateIsDirty();
            }
        }

        public float Z
        {
            get => _value.Z;
            set
            {
                if (value == _value.Z)
                    return;

                _value.Z = value;
                RaisePropertyChanged();
                UpdateIsDirty();
            }
        }

        public float W
        {
            get => _value.W;
            set
            {
                if (value == _value.W)
                    return;

                _value.W = value;
                RaisePropertyChanged();
                UpdateIsDirty();
            }
        }

        public bool Dirty
        {
            get => _dirty;
            private set
            {
                if (value == _dirty)
                    return;

                _dirty = value;
                RaisePropertyChanged();
            }
        }

        public void MarkClean()
        {
            _originalValue.X = X;
            _originalValue.Y = Y;
            _originalValue.Z = Z;
            _originalValue.W = W;
            Dirty = false;
        }

        public void Reset()
        {
            X = _originalValue.X;
            Y = _originalValue.Y;
            Z = _originalValue.Z;
            W = _originalValue.W;
            Dirty = false;
        }

        private void UpdateIsDirty()
        {
            Dirty = _value.X != _originalValue.X ||
                    _value.Y != _originalValue.Y ||
                    _value.Z != _originalValue.Z ||
                    _value.W != _originalValue.W;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
