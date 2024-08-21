using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Diagnostics;

string orangeColor = "\u001b[33m";
string greenColor = "\u001b[32m";
string redColor = "\u001b[31m";
string cyanColor = "\u001b[36m";
string resetColor = "\u001b[0m";

string banner = orangeColor + @"

██╗   ██╗ ██████╗ ██╗   ██╗ ██████╗██╗   ██╗██████╗ ███████╗
╚██╗ ██╔╝██╔═══██╗██║   ██║██╔════╝██║   ██║██╔══██╗██╔════╝
 ╚████╔╝ ██║   ██║██║   ██║██║     ██║   ██║██████╔╝█████╗  
  ╚██╔╝  ██║   ██║██║   ██║██║     ██║   ██║██╔══██╗██╔══╝  
   ██║   ╚██████╔╝╚██████╔╝╚██████╗╚██████╔╝██████╔╝███████╗ 
                                          by: Diego Pereira" + resetColor + "\n";

Console.Title = "YouCube: Seu downloader de vídeos do Youtube";
Console.WriteLine(banner + "\n");

while (true)
{
    Console.WriteLine(cyanColor + "Cole o link do vídeo do YouTube que você deseja baixar:" + resetColor);
    string videoUrl = Console.ReadLine();

    var youtube = new YoutubeClient();

    try
    {
        var video = await youtube.Videos.GetAsync(videoUrl);

        if (video != null)
        {
            Console.WriteLine($"\n{cyanColor}Baixando: {video.Title}" + resetColor);

            // Limpar o título do vídeo para que possa ser usado como nome de arquivo
            string sanitizedTitle = Regex.Replace(video.Title, @"[<>:""/\\|?*]", string.Empty);

            // Obter o manifesto de streams
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);

            // Combinar todas as streams de vídeo e áudio
            var videoStreams = streamManifest.GetVideoOnlyStreams().ToList();
            var audioStream = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            if (videoStreams.Any() && audioStream != null)
            {
                // Exibir as opções de qualidade de vídeo
                Console.WriteLine(cyanColor + "\nEscolha a qualidade do vídeo:" + resetColor);
                for (int i = 0; i < videoStreams.Count; i++)
                {
                    var stream = videoStreams[i];
                    Console.WriteLine($"{i + 1}. {stream.VideoQuality.Label} ({stream.Container.Name})");
                }

                // Obter a escolha do usuário
                Console.Write(cyanColor + "Digite o número da qualidade desejada: " + resetColor);
                int choice;
                while (!int.TryParse(Console.ReadLine(), out choice) || choice < 1 || choice > videoStreams.Count)
                {
                    Console.WriteLine(redColor + "Escolha inválida, tente novamente." + resetColor);
                    Console.Write(redColor + "Digite o número da qualidade desejada: " + resetColor);
                }

                // Selecionar o stream de vídeo escolhido
                var selectedVideoStream = videoStreams[choice - 1];

                var downloadsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
                Directory.CreateDirectory(downloadsFolder);

                var videoFilePath = Path.Combine(downloadsFolder, $"{sanitizedTitle}_video.{selectedVideoStream.Container.Name}");
                var audioFilePath = Path.Combine(downloadsFolder, $"{sanitizedTitle}_audio.{audioStream.Container.Name}");
                var finalFilePath = Path.Combine(downloadsFolder, $"{sanitizedTitle}.{selectedVideoStream.Container.Name}");
                string relativeFilePath = Path.GetRelativePath(downloadsFolder, finalFilePath);

                // Barra de progresso única
                var totalSteps = 3; // Baixar vídeo, baixar áudio e combinar
                var currentStep = 0;

                var progress = new Progress<double>(percent =>
                {
                    // Atualizar a exibição do progresso de forma geral
                    Console.Write($"\rProgresso: {percent:F1}%");
                });

                // Função para atualizar o progresso
                void UpdateProgress(int step, TimeSpan elapsedTime, TimeSpan totalDuration)
                {
                    // Exibir a mensagem de progresso sem o tempo restante
                    Console.Write(orangeColor + $"\rBaixando vídeo, aguarde um momento..." + resetColor);
                }

                // Download do vídeo
                var videoDownloadStart = DateTime.Now;
                await youtube.Videos.Streams.DownloadAsync(selectedVideoStream, videoFilePath, progress: new Progress<double>(percent =>
                {
                    TimeSpan elapsedTime = DateTime.Now - videoDownloadStart;
                    TimeSpan totalDuration = TimeSpan.FromSeconds((elapsedTime.TotalSeconds / percent) * 100);
                    UpdateProgress(currentStep, elapsedTime, totalDuration);
                }));

                currentStep++;
                UpdateProgress(currentStep, TimeSpan.Zero, TimeSpan.Zero);

                // Download do áudio
                var audioDownloadStart = DateTime.Now;
                await youtube.Videos.Streams.DownloadAsync(audioStream, audioFilePath, progress: new Progress<double>(percent =>
                {
                    TimeSpan elapsedTime = DateTime.Now - audioDownloadStart;
                    TimeSpan totalDuration = TimeSpan.FromSeconds((elapsedTime.TotalSeconds / percent) * 100);
                    UpdateProgress(currentStep, elapsedTime, totalDuration);
                }));

                currentStep++;
                UpdateProgress(currentStep, TimeSpan.Zero, TimeSpan.Zero);

                // Combinar vídeo e áudio usando FFmpeg
                var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                var ffmpegArgs = $"-i \"{videoFilePath}\" -i \"{audioFilePath}\" -c:v copy -c:a aac -b:a 192k -shortest \"{finalFilePath}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = ffmpegArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                TimeSpan ffmpegStartTime = DateTime.Now.TimeOfDay;
                TimeSpan ffmpegTotalDuration = TimeSpan.Zero;

                using (var streamReader = process.StandardError)
                {
                    string line;
                    while ((line = await streamReader.ReadLineAsync()) != null)
                    {
                        if (line.Contains("Duration:"))
                        {
                            var durationMatch = Regex.Match(line, @"Duration: (\d+):(\d+):(\d+.\d+)");
                            if (durationMatch.Success)
                            {
                                double hours = double.Parse(durationMatch.Groups[1].Value);
                                double minutes = double.Parse(durationMatch.Groups[2].Value);
                                double seconds = double.Parse(durationMatch.Groups[3].Value);
                                ffmpegTotalDuration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
                            }
                        }

                        if (line.Contains("time="))
                        {
                            var timeMatch = Regex.Match(line, @"time=(\d+):(\d+):(\d+.\d+)");
                            if (timeMatch.Success)
                            {
                                double hours = double.Parse(timeMatch.Groups[1].Value);
                                double minutes = double.Parse(timeMatch.Groups[2].Value);
                                double seconds = double.Parse(timeMatch.Groups[3].Value);
                                TimeSpan currentTime = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);

                                TimeSpan elapsedTime = DateTime.Now.TimeOfDay - ffmpegStartTime;
                                UpdateProgress(currentStep, elapsedTime, ffmpegTotalDuration);
                            }
                        }
                    }
                }

                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Excluir os arquivos temporários de vídeo e áudio
                    File.Delete(videoFilePath);
                    File.Delete(audioFilePath);

                    Console.WriteLine($"{greenColor}\nO vídeo foi salvo em: Downloads\\{relativeFilePath}{resetColor}");

                }
                else
                {
                    Console.WriteLine(redColor + "\nHouve um erro ao combinar o vídeo e o áudio." + resetColor);
                }
            }
            else
            {
                Console.WriteLine(redColor + "\nNão foi possível encontrar uma stream de vídeo adequada." + resetColor);
            }
        }
        else
        {
            Console.WriteLine(redColor + "\nNão foi possível obter informações sobre o vídeo do YouTube." + resetColor);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(redColor + $"\nOcorreu um erro: {ex.Message}" + resetColor);
    }

    // Perguntar ao usuário se deseja baixar outro vídeo
    Console.Write(cyanColor + "\nDeseja baixar outro vídeo? (S/N): " + resetColor);
    string response = Console.ReadLine().Trim().ToUpper();
    if (response != "S")
    {
        break;
    }
}
