﻿using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grpc.Core;
using v2ray.Core.App.Stats.Command;
using x2tap.Properties;
using x2tap.Utils;
using x2tap.View.Server;

namespace x2tap.View
{
    public partial class MainForm : Form
    {
        /// <summary>
        ///     流量
        /// </summary>
        public long Bandwidth;

        /// <summary>
        ///     启动状态
        /// </summary>
        public bool Started;

        /// <summary>
        ///     装填信息
        /// </summary>
        public string Status = "请下达命令！";

        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     初始化代理
        /// </summary>
        public void InitProxies()
        {
            // 先清空掉内容
            ProxyComboBox.Items.Clear();
            // 添加 v2ray 代理
            foreach (var v2ray in Global.v2rayProxies)
            {
                ProxyComboBox.Items.Add(string.Format("[v2ray] {0}", v2ray.Remark));
            }

            // 添加 Shadowsocks 代理
            foreach (var shadowsocks in Global.ShadowsocksProxies)
            {
                ProxyComboBox.Items.Add(string.Format("[Shadowsocks] {0}", shadowsocks.Remark));
            }

            if (ProxyComboBox.Items.Count > 0)
            {
                ProxyComboBox.SelectedIndex = 0;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // 初始化配置
            Config.InitFromFile();

            // 检查 TUN/TAP 适配器
            if (TUNTAP.GetComponentId() == "")
            {
                MessageBox.Show("未检测到 TUN/TAP 适配器，请检查 TAP-Windows 是否正确安装！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // 初始化代理
            InitProxies();
            if (ProxyComboBox.Items.Count > 0)
            {
                ProxyComboBox.SelectedIndex = 0;
            }

            // 初始化模式
            ModeComboBox.SelectedIndex = 0;

            // 后台工作
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        Invoke(new MethodInvoker(() =>
                        {
                            // 更新标题栏时间
                            Text = string.Format("x2tap - {0}", DateTime.Now.ToString());

                            // 更新状态信息
                            StatusLabel.Text = string.Format("状态：{0}", Status);
                        }));

                        // 更新流量信息
                        if (Started)
                        {
                            // 创建客户端实例
                            var client = new StatsService.StatsServiceClient(new Channel("127.0.0.1:2811", ChannelCredentials.Insecure));

                            // 获取并重置 上行/下行 统计信息
                            var uplink = client.GetStats(new GetStatsRequest {Name = "inbound>>>defaultInbound>>>traffic>>>uplink", Reset = true});
                            var downlink = client.GetStats(new GetStatsRequest {Name = "inbound>>>defaultInbound>>>traffic>>>downlink", Reset = true});

                            // 加入总流量
                            Bandwidth += uplink.Stat.Value;
                            Bandwidth += downlink.Stat.Value;

                            // 更新流量信息
                            Invoke(new MethodInvoker(() =>
                            {
                                UsedBandwidthLabel.Text = string.Format("已使用：{0}", Util.ComputeBandwidth(Bandwidth));
                                UplinkSpeedLabel.Text = string.Format("↑：{0}/s", Util.ComputeBandwidth(uplink.Stat.Value));
                                DownlinkSpeedLabel.Text = string.Format("↓：{0}/s", Util.ComputeBandwidth(downlink.Stat.Value));
                            }));
                        }
                        else
                        {
                            UsedBandwidthLabel.Text = "已使用：0 KB";
                            UplinkSpeedLabel.Text = "↑：0 KB/s";
                            DownlinkSpeedLabel.Text = "↓：0 KB/s";
                        }
                    }
                    catch (Exception)
                    {
                    }

                    // 休眠 100 毫秒
                    Thread.Sleep(100);
                }
            });
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Started)
            {
                e.Cancel = true;

                MessageBox.Show("请先点击关闭按钮", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                Config.SaveToFile();
            }
        }

        private void ComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            var cbx = sender as ComboBox;
            if (cbx != null)
            {
                e.DrawBackground();

                if (e.Index >= 0)
                {
                    var sf = new StringFormat();
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Alignment = StringAlignment.Center;

                    var brush = new SolidBrush(cbx.ForeColor);

                    if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                    {
                        brush = SystemBrushes.HighlightText as SolidBrush;
                    }

                    e.Graphics.DrawString(cbx.Items[e.Index].ToString(), cbx.Font, brush, e.Bounds, sf);
                }
            }
        }

        private void Addv2rayServerButton_Click(object sender, EventArgs e)
        {
            (Global.Views.Server.v2ray = new Server.v2ray()).Show();
            Hide();
        }

        private void AddShadowsocksButton_Click(object sender, EventArgs e)
        {
            (Global.Views.Server.Shadowsocks = new Shadowsocks()).Show();
            Hide();
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            var index = ProxyComboBox.SelectedIndex;
            if (index != -1)
            {
                ProxyComboBox.Items.RemoveAt(index);

                if (index < Global.v2rayProxies.Count)
                {
                    Global.v2rayProxies.RemoveAt(index);
                }
                else
                {
                    Global.ShadowsocksProxies.RemoveAt(index - Global.v2rayProxies.Count);
                }

                if (ProxyComboBox.Items.Count < index)
                {
                    ProxyComboBox.SelectedIndex = index;
                }
                else if (ProxyComboBox.Items.Count == 1)
                {
                    ProxyComboBox.SelectedIndex = 0;
                }
                else
                {
                    ProxyComboBox.SelectedIndex = index - 1;
                }
            }
            else
            {
                MessageBox.Show("请选择一个代理", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            if (ProxyComboBox.SelectedIndex != -1)
            {
                if (ProxyComboBox.SelectedIndex < Global.v2rayProxies.Count)
                {
                    (Global.Views.Server.v2ray = new Server.v2ray(true, ProxyComboBox.SelectedIndex)).Show();
                }
                else
                {
                    (Global.Views.Server.Shadowsocks = new Shadowsocks(true, ProxyComboBox.SelectedIndex - Global.v2rayProxies.Count)).Show();
                }

                Hide();
            }
            else
            {
                MessageBox.Show("请选择一个代理", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SubscribeButton_Click(object sender, EventArgs e)
        {
            (Global.Views.SubscribeForm = new SubscribeForm()).Show();
            Hide();
        }

        private void AdvancedButton_Click(object sender, EventArgs e)
        {
            (Global.Views.AdvancedForm = new AdvancedForm()).Show();
            Hide();
        }

        private void ControlButton_Click(object sender, EventArgs e)
        {
            if (!Started)
            {
                if (ProxyComboBox.SelectedIndex != -1)
                {
                    ProxyComboBox.Enabled = false;
                    ModeComboBox.Enabled = false;
                    ControlButton.Text = "执行中";
                    ControlButton.Enabled = false;

                    Task.Run(() =>
                    {
                        Thread.Sleep(1000);
                        Status = "正在生成配置文件中";
                        Invoke(new MethodInvoker(() =>
                        {
                            if (ModeComboBox.SelectedIndex == 0)
                            {
                                File.WriteAllText("v2ray.txt", ProxyComboBox.Text.StartsWith("[v2ray]") ? v2rayConfig(Encoding.UTF8.GetString(Resources.v2rayWithBypassChina)) : ShadowsocksConfig(Encoding.UTF8.GetString(Resources.ShadowsocksWithBypassChina)));
                            }
                            else
                            {
                                File.WriteAllText("v2ray.txt", ProxyComboBox.Text.StartsWith("[v2ray]") ? v2rayConfig(Encoding.UTF8.GetString(Resources.v2rayWithoutBypassChina)) : ShadowsocksConfig(Encoding.UTF8.GetString(Resources.ShadowsocksWithoutBypassChina)));
                            }
                        }));

                        Thread.Sleep(1000);
                        Status = "已启动，请自行检查网络是否正常";
                        Started = true;
                        Invoke(new MethodInvoker(() =>
                        {
                            ProxyComboBox.Enabled = true;
                            ModeComboBox.Enabled = true;
                            ControlButton.Text = "停止";
                            ControlButton.Enabled = true;
                        }));
                    });
                }
                else
                {
                    MessageBox.Show("请选择一个代理", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                ControlButton.Text = "执行中";
                ControlButton.Enabled = false;

                Task.Run(() =>
                {
                    Thread.Sleep(1000);
                    Status = "已停止";
                    Started = false;
                    Invoke(new MethodInvoker(() =>
                    {
                        ControlButton.Text = "启动";
                        ControlButton.Enabled = true;
                    }));
                });
            }
        }

        private void ProjectLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Shell.ExecuteCommandNoWait("start", "https://github.com/hacking001/x2tap");
        }

        public string v2rayConfig(string text)
        {
            // v2ray 日志等级
            switch (Global.Config.v2rayLoggingLevel)
            {
                case 0:
                    text = text.Replace("v2rayLoggingLevel", "debug");
                    break;
                case 1:
                    text = text.Replace("v2rayLoggingLevel", "info");
                    break;
                case 2:
                    text = text.Replace("v2rayLoggingLevel", "warning");
                    break;
                case 3:
                    text = text.Replace("v2rayLoggingLevel", "error");
                    break;
                case 4:
                    text = text.Replace("v2rayLoggingLevel", "none");
                    break;
                default:
                    text = text.Replace("v2rayLoggingLevel", "warning");
                    break;
            }

            // v2ray 出口绑定地址
            using (var client = new UdpClient("114.114.114.114", 53))
            {
                text = text.Replace("AdapterAddress", ((IPEndPoint) client.Client.LocalEndPoint).Address.ToString());
            }

            // v2ray 地址
            text = text.Replace("v2rayAddress", Global.v2rayProxies[ProxyComboBox.SelectedIndex].Address);

            // v2ray 端口
            text = text.Replace("v2rayPort", Global.v2rayProxies[ProxyComboBox.SelectedIndex].Port.ToString());

            // v2ray 用户 ID
            text = text.Replace("v2rayUserID", Global.v2rayProxies[ProxyComboBox.SelectedIndex].UserID);

            // v2ray 额外 ID
            text = text.Replace("v2rayAlterID", Global.v2rayProxies[ProxyComboBox.SelectedIndex].AlterID.ToString());

            // v2ray 加密方式
            switch (Global.v2rayProxies[ProxyComboBox.SelectedIndex].EncryptMethod)
            {
                case 0:
                    text = text.Replace("v2rayEncryptMethod", "chacha20-poly1305");
                    break;
                case 1:
                    text = text.Replace("v2rayEncryptMethod", "aes-128-gcm");
                    break;
                case 2:
                    text = text.Replace("v2rayEncryptMethod", "auto");
                    break;
                case 3:
                    text = text.Replace("v2rayEncryptMethod", "none");
                    break;
                default:
                    text = text.Replace("v2rayEncryptMethod", "chacha20-poly1305");
                    break;
            }

            // v2ray 传输协议
            switch (Global.v2rayProxies[ProxyComboBox.SelectedIndex].TransferProtocol)
            {
                case 0:
                    text = text.Replace("v2rayTransferProtocol", "tcp");
                    break;
                case 1:
                    text = text.Replace("v2rayTransferProtocol", "mkcp");
                    break;
                case 2:
                    text = text.Replace("v2rayTransferProtocol", "ws");
                    break;
                case 3:
                    text = text.Replace("v2rayTransferProtocol", "http");
                    break;
                default:
                    text = text.Replace("v2rayTransferProtocol", "tcp");
                    break;
            }

            // v2ray TLS 底层传输安全
            if (Global.v2rayProxies[ProxyComboBox.SelectedIndex].TLSSecure)
            {
                text = text.Replace("v2rayTLSSecure", "tls");
            }
            else
            {
                text = text.Replace("v2rayTLSSecure", "none");
            }

            // v2ray 伪装类型
            switch (Global.v2rayProxies[ProxyComboBox.SelectedIndex].FakeType)
            {
                case 0:
                    text = text.Replace("v2rayFakeType", "none");
                    break;
                case 1:
                    text = new Regex("v2rayFakeType").Replace(text, "http", 1);

                    text = text.Replace("v2rayFakeType", "none");
                    text = text.Replace("v2rayFakeDomain", Global.v2rayProxies[ProxyComboBox.SelectedIndex].FakeDomain);
                    break;
                case 2:
                    text = new Regex("v2rayFakeType").Replace(text, "none", 1);

                    text = text.Replace("v2rayFakeType", "srtp");
                    break;
                case 3:
                    text = new Regex("v2rayFakeType").Replace(text, "none", 1);

                    text = text.Replace("v2rayFakeType", "utp");
                    break;
                case 4:
                    text = new Regex("v2rayFakeType").Replace(text, "none", 1);

                    text = text.Replace("v2rayFakeType", "wechat-video");
                    break;
                case 5:
                    text = new Regex("v2rayFakeType").Replace(text, "none", 1);

                    text = text.Replace("v2rayFakeType", "dtls");
                    break;
                case 6:
                    text = new Regex("v2rayFakeType").Replace(text, "none", 1);

                    text = text.Replace("v2rayFakeType", "wireguard");
                    break;
                default:
                    text = text.Replace("v2rayFakeType", "none");
                    break;
            }

            // v2ray 路径
            text = text.Replace("v2rayPath", Global.v2rayProxies[ProxyComboBox.SelectedIndex].Path);

            return text;
        }

        public string ShadowsocksConfig(string text)
        {
            // v2ray 日志等级
            switch (Global.Config.v2rayLoggingLevel)
            {
                case 0:
                    text = text.Replace("v2rayLoggingLevel", "debug");
                    break;
                case 1:
                    text = text.Replace("v2rayLoggingLevel", "info");
                    break;
                case 2:
                    text = text.Replace("v2rayLoggingLevel", "warning");
                    break;
                case 3:
                    text = text.Replace("v2rayLoggingLevel", "error");
                    break;
                case 4:
                    text = text.Replace("v2rayLoggingLevel", "none");
                    break;
                default:
                    text = text.Replace("v2rayLoggingLevel", "warning");
                    break;
            }

            // v2ray 出口绑定地址
            using (var client = new UdpClient("114.114.114.114", 53))
            {
                text = text.Replace("AdapterAddress", ((IPEndPoint)client.Client.LocalEndPoint).Address.ToString());
            }

            // Shadowsocks 地址
            text = text.Replace("ShadowsocksAddress", Global.ShadowsocksProxies[ProxyComboBox.SelectedIndex - Global.v2rayProxies.Count].Address);

            // Shadowsocks 端口
            text = text.Replace("ShadowsocksPort", Global.ShadowsocksProxies[ProxyComboBox.SelectedIndex - Global.v2rayProxies.Count].Port.ToString());

            // Shadowsocks 加密方式
            switch (Global.ShadowsocksProxies[ProxyComboBox.SelectedIndex - Global.v2rayProxies.Count].EncryptMethod)
            {
                case 0:
                    text = text.Replace("ShadowsocksEncryptMethod", "aes-256-cfb");
                    break;
                case 1:
                    text = text.Replace("ShadowsocksEncryptMethod", "aes-128-cfb");
                    break;
                case 2:
                    text = text.Replace("ShadowsocksEncryptMethod", "chacha20");
                    break;
                case 3:
                    text = text.Replace("ShadowsocksEncryptMethod", "chacha20-ietf");
                    break;
                case 4:
                    text = text.Replace("ShadowsocksEncryptMethod", "aes-256-gcm");
                    break;
                case 5:
                    text = text.Replace("ShadowsocksEncryptMethod", "aes-128-gcm");
                    break;
                case 6:
                    text = text.Replace("ShadowsocksEncryptMethod", "chacha20-poly1305");
                    break;
                default:
                    text = text.Replace("ShadowsocksEncryptMethod", "aes-256-cfb");
                    break;
            }
            
            // Shadowsocks 密码
            text = text.Replace("ShadowsocksPassword", Global.ShadowsocksProxies[ProxyComboBox.SelectedIndex - Global.v2rayProxies.Count].Password);

            return text;
        }
    }
}