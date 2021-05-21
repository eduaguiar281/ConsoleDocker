using Docker.DotNet;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DockerWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IDockerClient _dockerClient;
        private const string WindowsUri = "npipe://./pipe/docker_engine";
        private const string UnixUri = "unix:///var/run/docker.sock";
        private bool ContainerAtivo = false;
        public MainWindow()
        {
            InitializeComponent();
            _dockerClient = new DockerClientConfiguration(SelectUriBasedOnEnv())
                .CreateClient();
        }

        private Uri SelectUriBasedOnEnv()
        {
            return IsRunningOnWindows() ? new Uri(WindowsUri) : new Uri(UnixUri);
        }

        private bool IsRunningOnWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        private async Task BaixarImagem(string imagem)
        {
            await _dockerClient.Images
                .CreateImageAsync(new ImagesCreateParameters
                {
                    FromImage = imagem
                },
                    null,
                    new Progress<JSONMessage>()
                );
        }

        private async Task<string> CriarContainer()
        {
            CreateContainerParameters parametros = CriarParametros();
            CreateContainerResponse container = await _dockerClient
                .Containers
                .CreateContainerAsync(parametros);

            return container.ID;
        }

        private CreateContainerParameters CriarParametros()
        {
            List<string> enviroments = new();
            foreach (var item in LstVariaveis.Items)
            {
                enviroments.Add((string)item);
            }
            return new CreateContainerParameters
            {
                Name = TxtNomeContainer.Text,
                Image = TxtImagem.Text,
                Env = enviroments,
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            $"{TxtPorta.Text}/tcp",
                            new PortBinding[]
                            {
                                new PortBinding
                                {
                                    HostPort = ObterPortaTcpLivre().ToString()
                                }
                            }
                        }
                    }
                }
            };
        }

        private int ObterPortaTcpLivre()
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
            return port;
        }

        private async Task InciarContainer(string containerId)
        {
            await _dockerClient
                .Containers
                .StartContainerAsync(containerId, new ContainerStartParameters());
            ContainerAtivo = true;
        }

        private async Task PararContainer(string containerId)
        {
            await _dockerClient.Containers
                .StopContainerAsync(containerId, new ContainerStopParameters());
            ContainerAtivo = false;
        }

        private async Task RemoverContainer(string containerId)
        {
            await _dockerClient.Containers
                .RemoveContainerAsync(containerId, new ContainerRemoveParameters());
        }

        private async Task<IList<ContainerListResponse>> ListarContainers()
        {
            return await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters());
        }

        private void ExibirContainers(IList<ContainerListResponse> containers)
        {
            LstContainers.Items.Clear();
            foreach (var container in containers)
            {
                string names = string.Join(",", container.Names);
                string ports = string.Join(",", container.Ports.Select(p => $"{p.PublicPort}:{p.PrivatePort}"));
                LstContainers.Items.Add($"{names} - {container.Image} - {ports} - {container.ID}");
            }
        }

        #region Métodos do Formulário
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            string tag = !string.IsNullOrEmpty(TxtTag.Text) ? TxtTag.Text : "latest";
            try
            {
                LblMensagem.Visibility = Visibility.Visible;
                await BaixarImagem($"{TxtImagem.Text}:{tag}");
                PnlContainer.IsEnabled = true;
            }
            finally
            {
                LblMensagem.Visibility = Visibility.Hidden;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            LstVariaveis.Items.Add(TxtVariaveis.Text);
        }

        private void LstVariaveis_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                foreach (var item in LstVariaveis.SelectedItems)
                {
                    LstVariaveis.Items.Remove(item);
                }
            }
        }

        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            TxtContainerID.Text = await CriarContainer();
            if (string.IsNullOrEmpty(TxtContainerID.Text))
                return;
            TxtNomeContainer2.Text = TxtNomeContainer.Text;
            TxtPorta1.Text = TxtPorta.Text;
            PnlContainer2.IsEnabled = true;
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (!ContainerAtivo)
            {
                await InciarContainer(TxtContainerID.Text);
                BtnIniciarParar.Content = "Parar";
            }
            else
            {
                await PararContainer(TxtContainerID.Text);
                BtnIniciarParar.Content = "Iniciar";
            }
        }

        private async void BtnRemover_OnClick(object sender, RoutedEventArgs e)
        {
            if (ContainerAtivo)
            {
                await PararContainer(TxtContainerID.Text);
                BtnIniciarParar.Content = "Iniciar";
            }
            await RemoverContainer(TxtContainerID.Text);
        }
        #endregion

        private async void Button_Click_4(object sender, RoutedEventArgs e)
        {
            ExibirContainers(await ListarContainers());
        }
    }
}
