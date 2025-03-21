namespace FakerGeneratorCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
            {
                Console.WriteLine("Usage: FakerGeneratorCLI <path_to_model_directory> <output_namespace> <output_path> [-r]");
                return;
            }

            string inputPath = args[0];
            string outputNamespace = args[1];
            string outputPath = args[2];
            bool recursive = args.Length == 4 && args[3] == "-r";

            int fileCount = 0;

            Directory.CreateDirectory(outputPath);

            if (Directory.Exists(inputPath))
            {
                string[] modelFiles = Directory.GetFiles(inputPath, "*.cs", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                foreach (string modelFilePath in modelFiles)
                {
                    ProcessModelFile(modelFilePath, outputNamespace, outputPath);
                    fileCount++;
                }
            }
            else
            {
                Console.WriteLine($"Error: Input path '{inputPath}' is not a valid directory.");
                return;
            }

            Console.WriteLine($"\nFaker code generation completed for {fileCount} model file(s).");
        }

        private static void ProcessModelFile(string modelFilePath, string outputNamespace, string outputDirectory)
        {
            try
            {
                string fakerCode = FakerGenerator.GenerateFakerCode(modelFilePath, outputNamespace);

                string modelFileNameWithoutExtension = Path.GetFileNameWithoutExtension(modelFilePath);
                string outputFileName = $"{modelFileNameWithoutExtension}Faker.cs";
                string outputFilePath = Path.Combine(outputDirectory, outputFileName);

                File.WriteAllText(outputFilePath, fakerCode);
                Console.WriteLine($"Faker code generated successfully for '{Path.GetFileName(modelFilePath)}' and saved to: {outputFilePath}");
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"Error: Model file not found at '{modelFilePath}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while processing '{Path.GetFileName(modelFilePath)}': {ex.Message}");
            }
        }
    }
}