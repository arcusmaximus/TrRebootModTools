using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TrRebootTools.BinaryTemplateGenerator
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            //ExtractActionGraphNodeArgs();
            //return;

            if (!TryParseArgs(args, out string? structName, out CdcGame game))
            {
                string assemblyName = Assembly.GetEntryAssembly()!.GetName().Name!;
                Console.WriteLine($"Usage: {assemblyName} <structure name> <TR version 9/10/11>");
                Console.WriteLine($"Example: {assemblyName} dtp::MeshRef 11");
                return;
            }

            string headerFilePath = Path.Combine(AppContext.BaseDirectory, $"TR{(int)game}.h");
            if (!File.Exists(headerFilePath))
            {
                Console.WriteLine($"Header file missing (expected at {headerFilePath}).");
                return;
            }

            string templateFilePath = $"tr{(int)game}{Regex.Replace(structName, @"^(\w+::)+", "").ToLower()}.bt";

            try
            {
                Console.WriteLine("Reading header file...");
                TypeLibrary lib;
                using (StreamReader reader = new(headerFilePath))
                {
                    lib = new HeaderReader(reader, game == CdcGame.Tr2013 ? 4 : 8).Read();
                }

                Console.WriteLine("Writing binary template...");
                lib.CalculateAlignmentsAndSizes(structName);
                using (StreamWriter writer = new(templateFilePath))
                {
                    new BinaryTemplateWriter(lib, writer, game).WriteRootType(lib.Types[structName]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static bool TryParseArgs(string[] args, [NotNullWhen(true)] out string? structName, out CdcGame game)
        {
            structName = null;
            game = 0;

            if (args.Length != 2)
                return false;

            structName = args[0];
            if (!int.TryParse(args[1], out int trVersion))
                return false;

            game = (CdcGame)trVersion;
            if (!Enum.IsDefined(game))
                return false;

            return true;
        }

        private static void ExtractActionGraphNodeArgs()
        {
            TypeLibrary lib;
            using (StreamReader reader = new(@"D:\Projects\TrRebootModTools\Build\Release\TR11.h"))
            {
                lib = new HeaderReader(reader, 8).Read();
            }

            JsonDocument slotsDoc;
            using (Stream slotsStream = File.OpenRead(@"D:\Projects\TrRebootModTools\_notes\action graph node slots.json"))
            {
                slotsDoc = JsonDocument.Parse(slotsStream);
            }
            
            var extractor = new ActionGraphNodeExtractor(lib, slotsDoc, CdcGame.Shadow);
            /*
            using (StreamWriter writer = new(@"D:\Projects\TrRebootModTools\Templates\tr11actiongraph-dispatch.bt"))
            {
                extractor.WriteCaseStatements(writer);
            }
            */
            StringWriter factory = new();
            extractor.WriteCSharpFactory(factory);

            extractor.WriteCSharpNodeTypes(@"D:\Projects\TrRebootModTools\ActionGraphEditor\Shadow\Nodes");
        }
    }
}
