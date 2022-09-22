﻿using StreamGlass.Twitch;
using StreamGlass.Twitch.IRC;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StreamGlass
{
    /// <summary>
    /// Interaction logic for ChatMessage.xaml
    /// </summary>
    public partial class ChatMessage : UserControl
    {
        private readonly BrushPaletteManager m_Palette;
        private readonly UserMessage m_Message;
        private readonly bool m_IsHighlighted;

        public ChatMessage(BrushPaletteManager palette, UserMessage message, bool isHighligted)
        {
            InitializeComponent();
            m_Palette = palette;
            m_Message = message;
            MessageSender.Text = message.UserName;
            BrushConverter converter = new();
            SolidColorBrush? color = (SolidColorBrush?)converter.ConvertFrom(message.Color);
            if (color != null)
                MessageSender.Foreground = color;
            MessageContent.Text = message.EmotelessMessage;
            m_IsHighlighted = (isHighligted || (message.SenderType > UserMessage.UserType.MOD && message.SenderType < UserMessage.UserType.MOD));
            UpdatePaletteColor();
        }

        private void UpdateEmotes()
        {
            EmoteLayer.Height = MessageContent.Height;
            EmoteLayer.Children.Clear();
            foreach (var emoteData in m_Message.Emotes)
            {
                Rect charRect = MessageContent.GetRectFromCharacterIndex(emoteData.Item1);
                double emoteX = charRect.X + 2 + MessageSender.Width + MessageSender.Margin.Left + MessageSender.Margin.Right + MessageContent.Margin.Left + Margin.Left;
                double emoteY = charRect.Y + MessageContent.Margin.Top + Margin.Top;
                string emoteURL = API.GetEmoteURL(emoteData.Item2, m_Palette.GetPaletteType());
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(emoteURL, UriKind.Absolute);
                bitmap.EndInit();
                EmoteLayer.Children.Add(new Image()
                {
                    Width = MessageContent.FontSize,
                    Height = MessageContent.FontSize,
                    Source = bitmap,
                    Margin = new(emoteX, emoteY, 0, 0)
                });
            }
        }

        private void UpdatePaletteColor()
        {
            if (m_IsHighlighted)
            {
                MessageContent.Foreground = m_Palette.GetColor("highlight_message");
                MessagePanel.Background = m_Palette.GetColor("highlight_background");
            }
            else
            {
                MessageContent.Foreground = m_Palette.GetColor("message");
                MessagePanel.Background = m_Palette.GetColor("background");
            }
        }

        internal void UpdatePalette()
        {
            UpdatePaletteColor();
            UpdateEmotes();
        }

        private void MessageContent_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateEmotes();
        }
    }
}