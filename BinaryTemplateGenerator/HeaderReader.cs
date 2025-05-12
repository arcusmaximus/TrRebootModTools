using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class HeaderReader
    {
        private const string AnnotationsRegex = "(?:(?: __[^ ]+)|(?: /\\*.*?\\*/))*";
        private const string TypeRegex = @"(?:unsigned )?(?:long )?(?:(?<open><)|(?<-open>>)|(?(open)[^<>])|[^ <>,])+(?: \*+)?";

        public static readonly Regex DeclarationRegex = new Regex(@$"^(?<keyword>struct|union|enum){AnnotationsRegex} (?<name>{TypeRegex})(?: : (?:(?=.)(?<baseTypes>{TypeRegex})(?:, |$))+)?$", RegexOptions.Compiled);
        private static readonly Regex TypedefRegex = new Regex($@"^typedef (?<source>{TypeRegex}) (?<target>{TypeRegex});$", RegexOptions.Compiled);
        private static readonly Regex FieldRegex = new Regex(@$"^  (?<type>{TypeRegex}) *(?<name>[<>\w]+)(?: : (?<bitLength>\d+))?(?:\[(?<arrayDimensions>\d*)\])*(?: */\*.*?\*/)?;$", RegexOptions.Compiled);
        private static readonly Regex EnumValueRegex = new Regex(@"^  (?<name>\w+) = 0x(?<value>\w+),$", RegexOptions.Compiled);
        private static readonly Regex IgnoredKeywordsRegex = new Regex(@"\b(?:const|volatile|(?!^)(?<!^const )(?<!^volatile )struct|__ptr32|__ptr64) ", RegexOptions.Compiled);

        private readonly TextReader _reader;
        private readonly int _pointerSize;

        public HeaderReader(TextReader reader, int pointerSize)
        {
            _reader = reader;
            _pointerSize = pointerSize;
        }

        public TypeLibrary Read()
        {
            TypeLibrary lib = new(_pointerSize);

            string? line;
            CCompositeType? currentComposite = null;
            CEnum? currentEnum = null;
            bool inComment = false;
            while ((line = _reader.ReadLine()) != null)
            {
                if (line.StartsWith("/*"))
                    inComment = true;

                if (line.EndsWith("*/"))
                {
                    inComment = false;
                    continue;
                }

                if (inComment)
                    continue;

                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                Match match;

                if (line.StartsWith("typedef "))
                {
                    if (!line.Contains("("))
                    {
                        match = TypedefRegex.Match(line);
                        if (match.Success && lib.Types.TryGetValue(match.Groups["source"].Value, out CType sourceType))
                            lib.Types[match.Groups["target"].Value] = sourceType;
                    }
                    continue;
                }

                if ((line.StartsWith("struct ") || line.StartsWith("union ")) && line.EndsWith(";"))
                    continue;

                if (currentComposite == null && currentEnum == null)
                {
                    line = IgnoredKeywordsRegex.Replace(line, "");
                    match = DeclarationRegex.Match(line);
                    if (match.Success)
                    {
                        string keyword = match.Groups["keyword"].Value;
                        string name = match.Groups["name"].Value;
                        string[] baseTypes = match.Groups["baseTypes"].Success ? match.Groups["baseTypes"].Captures.Cast<Capture>().Select(c => c.Value).ToArray() : [];
                        switch (keyword)
                        {
                            case "struct":
                                currentComposite = new CStructure(name, baseTypes);
                                lib.Add(currentComposite);
                                break;

                            case "union":
                                currentComposite = new CUnion(name);
                                lib.Add(currentComposite);
                                break;

                            case "enum":
                                currentEnum = new CEnum(name, baseTypes.Length > 0 ? baseTypes[0] : null);
                                lib.Add(currentEnum);
                                break;
                        }

                        if (_reader.ReadLine() != "{")
                            throw new InvalidDataException();

                        continue;
                    }
                }
                else if (line == "};")
                {
                    currentComposite = null;
                    currentEnum = null;
                    continue;
                }
                else if (currentComposite != null)
                {
                    if (line.Contains("("))
                        continue;

                    line = IgnoredKeywordsRegex.Replace(line, "");
                    match = FieldRegex.Match(line);
                    if (!match.Success)
                        throw new InvalidDataException();

                    string type = match.Groups["type"].Value;
                    string name = match.Groups["name"].Value;
                    int? bitLength = match.Groups["bitLength"].Success ? int.Parse(match.Groups["bitLength"].Value) : null;

                    Group arrayDimensionsGroup = match.Groups["arrayDimensions"];
                    int[] arrayDimensions = arrayDimensionsGroup.Success ? arrayDimensionsGroup.Captures.Cast<Capture>().Select(c => c.Length > 0 ? int.Parse(c.Value) : 0).ToArray()
                                                                         : [];
                    
                    currentComposite.Fields.Add(new CField(type, name, bitLength, arrayDimensions));
                    continue;
                }
                else if (currentEnum != null)
                {
                    match = EnumValueRegex.Match(line);
                    if (!match.Success)
                        throw new InvalidDataException();

                    string name = match.Groups["name"].Value;
                    int value = int.Parse(match.Groups["value"].Value, NumberStyles.AllowHexSpecifier);
                    currentEnum.Values.Add(name, value);
                    continue;
                }

                throw new InvalidDataException();
            }

            return lib;
        }
    }
}
