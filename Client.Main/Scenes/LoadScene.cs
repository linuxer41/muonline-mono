using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Client.Main.Controls.UI;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO.Compression;
using System.Linq;

namespace Client.Main.Scenes
{
    public class Metadata
    {
        public long TotalSize { get; set; }
        public double Version { get; set; }
        public required List<FileMetadata> Files { get; set; }
    }

    public class FileMetadata
    {
        public required string Path { get; set; }
        public required long Size { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Metadata))]
    [JsonSerializable(typeof(List<FileMetadata>))]
    public partial class MetadataContext : JsonSerializerContext
    {
    }

    public class LoadScene : BaseScene
    {
        private LabelControl _stepLabel;
        private LabelControl _statusLabel;
        private float _progress; // Valor entre 0 y 1
        private string _statusText;
        private bool _isDownloadComplete;

        // Textura del fondo
        private Texture2D _backgroundTexture;

        // Barra de progreso
        private const int ProgressBarWidth = 600;
        private const int ProgressBarHeight = 30;
        private const int ProgressBarX = 100;
        private const int ProgressBarY = 400;

        // BasicEffect para dibujar
        private BasicEffect _basicEffect;

        private string pathUrl = Constants.PathUrl;

        private string localIndexPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "index.json");
        private string localFilesPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data", "files.json");
        private string extractPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data");

        public LoadScene()
        {
            _progress = 0f;
            _statusText = "Initializing...";
            _isDownloadComplete = false;

            // Inicializar la etiqueta de estado
            _statusLabel = new LabelControl
            {
                Text = _statusText,
                X = 50,
                Y = MuGame.Instance.Height - 50, // Ajustar posición según la altura de la ventana
                FontSize = 12,
                TextColor = Color.White
            };

            _stepLabel = new LabelControl
            {
                Text = "Step 1/3  Checking version...",
                X = 50,
                Y = MuGame.Instance.Height - 70, // Ajustar posición según la altura de la ventana
                FontSize = 12,
                TextColor = Color.White
            };

            // Agregar controles a la escena
            Controls.Add(_stepLabel);
            Controls.Add(_statusLabel);
        }

        public override async Task Load()
        {
            Console.WriteLine("LoadScene.Load");

            // Cargar la textura del fondo
            _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");

            // Inicializar BasicEffect
            _basicEffect = new BasicEffect(MuGame.Instance.GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, MuGame.Instance.Width, MuGame.Instance.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity
            };

            await ChangeWorldAsync<LoadWorld>();
            await base.Load();
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            StartDownloadingAssets();
        }

        private async void StartDownloadingAssets()
        {
            try
            {
                Console.WriteLine($"ExtractPath: {extractPath}");
                await DownloadZipAssets();
                await VerifyIndividualAssets();

                await Task.Delay(500);
                MuGame.Instance.ChangeScene<LoginScene>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during download or extraction: {ex.Message}");
                _statusText = $"Download failed! {ex.Message}";
                _statusLabel.Text = _statusText;
            }
        }

        private async Task DownloadZipAssets()
        {
            _stepLabel.Text = "Step 2/3  Downloading zip assets...";
            string indexContent = await DownloadStringAsync($"{pathUrl}/index.json");
            Console.WriteLine(indexContent);
            var options = new JsonSerializerOptions
            {

                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };
            var indexData = JsonSerializer.Deserialize<Metadata>(indexContent, MetadataContext.Default.Metadata);

            // Paso 2: Verificar si el archivo index.json local existe y comparar versiones
            if (File.Exists(localIndexPath))
            {
                string localIndexContent = await File.ReadAllTextAsync(localIndexPath);
                var localIndexData = JsonSerializer.Deserialize<Metadata>(localIndexContent, MetadataContext.Default.Metadata);

                if (localIndexData.Version >= indexData.Version)
                {
                    _statusText = "Assets are up to date. Skipping download.";
                    _statusLabel.Text = _statusText;
                    return;
                }
            }

            int totalFiles = indexData.Files.Count;
            int downloadedFiles = 0;
            long downloadedBytes = 0;

            _statusText = $"Downloading {totalFiles} assets...";
            _statusLabel.Text = _statusText;

            foreach (var file in indexData.Files)
            {
                string fileName = file.Path;
                string filePath = Path.Combine(extractPath, fileName);
                string fileUrl = $"{pathUrl}/Assets/{fileName}";
                Console.WriteLine($"Downloading {fileName}");

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                (downloadedBytes, downloadedFiles) = await DownloadFileWithProgress(
                    fileUrl, filePath, indexData.TotalSize, downloadedBytes, downloadedFiles, totalFiles);
                    
                string zipPath = Path.Combine(extractPath, fileName);
                // extract zip in background thread, if is the last file extrar in main thread
                if (file == indexData.Files.Last())
                {

                    await ExtractWithProgress(zipPath, extractPath);
                }
                else
                {
                    // extract zip in background thread
                    _ = Task.Run(() => ExtractWithProgress(zipPath, extractPath));
                }
            }
            await File.WriteAllTextAsync(localIndexPath, indexContent);
        }

        private async Task VerifyIndividualAssets()
        {
             _stepLabel.Text = "Step 3/3  Downloading missing assets...";
            string filesContent = await DownloadStringAsync($"{pathUrl}/Assets/files.json");
            var filesData = JsonSerializer.Deserialize<Metadata>(filesContent, MetadataContext.Default.Metadata);
            Console.WriteLine($"Downloaded files.json");
            Console.WriteLine(filesData);
            List<FileMetadata> missingOrUpdatedFiles = new List<FileMetadata>();
            foreach (var file in filesData.Files)
            {
                string localFilePath = Path.Combine(extractPath, file.Path);
                if (!File.Exists(localFilePath) || new FileInfo(localFilePath).Length != file.Size)
                {
                    missingOrUpdatedFiles.Add(file);
                }
            }
            if (missingOrUpdatedFiles.Count == 0)
            {
                _statusText = "All assets are up to date.";
                _statusLabel.Text = _statusText;
            }

            int totalFiles = missingOrUpdatedFiles.Count;
            long totalSize = missingOrUpdatedFiles.Sum(x => x.Size);
            int downloadedFiles = 0;
            long downloadedBytes = 0;

            _statusText = $"Downloading incomplete assets...";
            _statusLabel.Text = _statusText;

            foreach (var file in missingOrUpdatedFiles)
            {
                string fileName = file.Path;
                string filePath = Path.Combine(extractPath, fileName);
                string fileUrl = $"{pathUrl}/Data/{fileName}";
                Console.WriteLine($"Downloading {fileUrl}");

                // Crear directorios si es necesario
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                // Descargar el archivo
                (downloadedBytes, downloadedFiles) = await DownloadFileWithProgress(
                    fileUrl, filePath, totalSize, downloadedBytes, downloadedFiles, totalFiles);
            }
            await File.WriteAllTextAsync(localFilesPath, filesContent);
        }

        private async Task<string> DownloadStringAsync(string url)
        {
            Console.WriteLine($"Downloading {url}");
            using (HttpClient client = new HttpClient())
            {
                return await client.GetStringAsync(url);
            }
        }

        private async Task<(long downloadedBytes, int downloadedFiles)> DownloadFileWithProgress(
            string url, string savePath, long totalSize, long downloadedBytes, int downloadedFiles, int totalFiles)
        {
            using (HttpClient client = new HttpClient())
            {
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long? fileSize = response.Content.Headers.ContentLength;
                    long receivedBytes = 0;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
                    {
                        byte[] buffer = new byte[81920];
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            receivedBytes += bytesRead;
                            downloadedBytes += bytesRead;

                            float fileProgress = fileSize.HasValue ? (float)receivedBytes / fileSize.Value : 0;
                            float totalProgress = totalSize > 0 ? (float)downloadedBytes / totalSize : 0;

                            // _statusText = $"Downloading assets... {downloadedFiles}/{totalFiles} ({(totalProgress * 100):F0}%) | Current: {(fileProgress * 100):F0}%";
                            _statusText = $"Downloading assets... {downloadedFiles}/{totalFiles} | Current: {(fileProgress * 100):F0}%";
                            _statusLabel.Text = _statusText;
                        }
                    }
                }

                downloadedFiles++;
                return (downloadedBytes, downloadedFiles);
            }
        }
        
        
        // stract zips with progress
        private Task ExtractWithProgress(string zipPath, string extractPath)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                int totalEntries = archive.Entries.Count;
                int extractedEntries = 0;

                foreach (var entry in archive.Entries)
                {
                    string fullPath = Path.Combine(extractPath, entry.FullName);

                    // Crear directorios si es necesario
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        Directory.CreateDirectory(fullPath);
                        continue;
                    }

                    // Extraer el archivo, reemplazando si ya existe
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);

                    // Actualizar el progreso
                    extractedEntries++;
                    float progress = (float)extractedEntries / totalEntries;
                    Console.WriteLine($"Extracting assets... {progress * 100:F0}%");
                    // _statusText = $"Extracting assets {zipPath}... {progress * 100:F0}%";
                    // _statusLabel.Text = _statusText;
                }
            }

            // delete zip file
            File.Delete(zipPath);

            return Task.CompletedTask;

        }
        public override void Draw(GameTime gameTime)
        {
            // Dibujar el fondo
            _DrawBackground();

            // Dibujar la barra de progreso
            // DrawProgressBar();

            // Dibujar los controles
            base.Draw(gameTime);
        }

        private void _DrawBackground()
        {
            if (_backgroundTexture != null)
            {
                // Definir los vértices para el fondo
                var vertices = new VertexPositionTexture[]
                {
                    new VertexPositionTexture(new Vector3(0, 0, 0), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(MuGame.Instance.Width, 0, 0), new Vector2(1, 0)),
                    new VertexPositionTexture(new Vector3(0, MuGame.Instance.Height, 0), new Vector2(0, 1)),
                    new VertexPositionTexture(new Vector3(MuGame.Instance.Width, MuGame.Instance.Height, 0), new Vector2(1, 1))
                };

                // Configurar BasicEffect
                _basicEffect.TextureEnabled = true;
                _basicEffect.Texture = _backgroundTexture;

                foreach (var pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    MuGame.Instance.GraphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleStrip,
                        vertices,
                        0,
                        2);
                }
            }
        }

        private void DrawProgressBar()
        {
            // Calcular el ancho actual de la barra de progreso
            int currentWidth = (int)(ProgressBarWidth * _progress);

            // Definir los vértices para el fondo de la barra de progreso
            var backgroundVertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(ProgressBarX, ProgressBarY, 0), Color.DarkGray),
                new VertexPositionColor(new Vector3(ProgressBarX + ProgressBarWidth, ProgressBarY, 0), Color.DarkGray),
                new VertexPositionColor(new Vector3(ProgressBarX, ProgressBarY + ProgressBarHeight, 0), Color.DarkGray),
                new VertexPositionColor(new Vector3(ProgressBarX + ProgressBarWidth, ProgressBarY + ProgressBarHeight, 0), Color.DarkGray)
            };

            // Definir los vértices para el progreso actual
            var progressVertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(ProgressBarX, ProgressBarY, 0), Color.Green),
                new VertexPositionColor(new Vector3(ProgressBarX + currentWidth, ProgressBarY, 0), Color.Green),
                new VertexPositionColor(new Vector3(ProgressBarX, ProgressBarY + ProgressBarHeight, 0), Color.Green),
                new VertexPositionColor(new Vector3(ProgressBarX + currentWidth, ProgressBarY + ProgressBarHeight, 0), Color.Green)
            };

            // Dibujar el fondo de la barra de progreso
            _basicEffect.TextureEnabled = false;
            _basicEffect.VertexColorEnabled = true;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                MuGame.Instance.GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleStrip,
                    backgroundVertices,
                    0,
                    2);
            }

            // Dibujar el progreso actual
            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                MuGame.Instance.GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleStrip,
                    progressVertices,
                    0,
                    2);
            }
        }
    }
}