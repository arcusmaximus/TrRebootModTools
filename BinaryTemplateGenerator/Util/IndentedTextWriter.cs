namespace TrRebootTools.BinaryTemplateGenerator.Util
{
    internal class IndentedTextWriter
    {
        private readonly TextWriter _inner;

        private bool _atLineStart = true;
        private int _indent;
        private string _indentString = "";

        public IndentedTextWriter(TextWriter inner)
        {
            _inner = inner;
        }

        public int Indent
        {
            get => _indent;
            set
            {
                _indent = value;
                _indentString = new string(' ', 4 * _indent);
            }
        }

        public void Write(string text)
        {
            if (text.Length == 0)
                return;

            if (_atLineStart)
                _inner.Write(_indentString);

            _inner.Write(text.Replace("\r\n", "\r\n" + _indentString));
            _atLineStart = text.EndsWith("\r\n");
        }

        public void WriteLine()
        {
            _inner.WriteLine();
            _atLineStart = true;
        }

        public void WriteLine(string text)
        {
            if (_atLineStart)
                _inner.Write(_indentString);

            _inner.WriteLine(text.Replace("\r\n", "\r\n" + _indentString));
            _atLineStart = true;
        }
    }
}
