using System;
using System.IO;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

string orangeColor = "\u001b[33m";
string resetColor = "\u001b[0m";

string banner = orangeColor + @"██╗   ██╗ ██████╗ ██╗   ██╗ ██████╗██╗   ██╗██████╗ ███████╗
╚██╗ ██╔╝██╔═══██╗██║   ██║██╔════╝██║   ██║██╔══██╗██╔════╝
 ╚████╔╝ ██║   ██║██║   ██║██║     ██║   ██║██████╔╝█████╗  
  ╚██╔╝  ██║   ██║██║   ██║██║     ██║   ██║██╔══██╗██╔══╝  
   ██║   ╚██████╔╝╚██████╔╝╚██████╗╚██████╔╝██████╔╝███████╗ 
                                              by: diwalker" + resetColor + "\n";

Console.WriteLine(banner + "\n");

Console.WriteLine("Insira a URL do vídeo do YouTube que você deseja baixar: ");
string videoUrl = Console.ReadLine();

var youtube = new YoutubeClient();
var video = await youtube.Videos.GetAsync(videoUrl);

if (video != null)
{
    Console.WriteLine($"Baixando: {video.Title}");

    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
    var streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();

    if (streamInfo != null)
    {
        var ext = streamInfo.Container.Name;
        var downloadsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
        Directory.CreateDirectory(downloadsFolder);

        var filePath = Path.Combine(downloadsFolder, $"{video.Title}.{ext}");

        await youtube.Videos.Streams.DownloadAsync(streamInfo, filePath);

        Console.WriteLine($"O vídeo foi baixado com sucesso em: {filePath}");
    }
    else
    {
        Console.WriteLine("Não foi possível encontrar uma stream de vídeo adequada.");
    }
}
else
{
    Console.WriteLine("Não foi possível obter informações sobre o vídeo do YouTube.");
}

Console.ReadLine();
