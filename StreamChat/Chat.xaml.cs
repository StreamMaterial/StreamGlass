﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using StreamFeedstock;
using StreamFeedstock.Controls;

namespace StreamGlass.StreamChat
{
    public partial class Chat : StreamFeedstock.Controls.UserControl
    {
        public enum DisplayType
        {
            TOP_TO_BOTTOM,
            REVERSED_TOP_TO_BOTTOM,
            BOTTOM_TO_TOP,
            REVERSED_BOTTOM_TO_TOP
        }

        private BrushPaletteManager m_ChatPalette = new();
        private TranslationManager m_Translations = new();
        private bool m_AutoScroll = true;
        private bool m_ForceAutoScroll = false;
        private bool m_Reversed = false;
        private bool m_IsOnBottom = false;
        private double m_MessageSenderFontSize = 14;
        private double m_MessageSenderWidth = 100;
        private double m_MessageContentFontSize = 14;
        private string m_CurrentChannel = "";
        private readonly Dictionary<string, List<Message>> m_ControlMessages = new();
        private readonly Dictionary<string, HashSet<string>> m_ChatHighlightedUsers = new();
        private readonly Dictionary<System.Windows.Controls.TextBox, Message> m_Converter = new();
        private IStreamChat? m_StreamChat = null;

        public Chat()
        {
            InitializeComponent();
            CanalManager.Register(StreamGlassCanals.CHAT_MESSAGE, (int _, UserMessage message) => OnChatMessage(message));
        }

        internal void SetBrushPalette(BrushPaletteManager colorPalette) => m_ChatPalette = colorPalette;

        internal void SetTranslations(TranslationManager translations) => m_Translations = translations;

        public void SetStreamChat(IStreamChat streamChat) => m_StreamChat = streamChat;

        public string GetEmoteURL(string id) => m_StreamChat!.GetEmoteURL(id, m_ChatPalette);

        internal double MessageSenderFontSize => m_MessageSenderFontSize;
        internal double MessageSenderWidth => m_MessageSenderWidth;
        internal double MessageContentFontSize => m_MessageContentFontSize;

        internal void SetChannel(string channel)
        {
            m_CurrentChannel = channel;
            Dispatcher.Invoke(() => UpdateMessagePosition());
        }

        internal void AddChannel(string channel)
        {
            m_ControlMessages[channel] = new();
            m_ChatHighlightedUsers[channel] = new();
        }

        internal void SetDisplayType(DisplayType displayType)
        {
            switch (displayType)
            {
                case DisplayType.TOP_TO_BOTTOM:
                    {
                        m_IsOnBottom = false;
                        m_Reversed = false;
                        break;
                    }
                case DisplayType.REVERSED_TOP_TO_BOTTOM:
                    {
                        m_IsOnBottom = false;
                        m_Reversed = true;
                        break;
                    }
                case DisplayType.BOTTOM_TO_TOP:
                    {
                        m_IsOnBottom = true;
                        m_Reversed = false;
                        break;
                    }
                case DisplayType.REVERSED_BOTTOM_TO_TOP:
                    {
                        m_IsOnBottom = true;
                        m_Reversed = true;
                        break;
                    }
            }
            UpdateMessagePosition();
            UpdateScrollBar(false);
        }

        internal void SetSenderWidth(double width)
        {
            m_MessageSenderWidth = width;
            if (m_ControlMessages.TryGetValue(m_CurrentChannel, out var chatMessageList))
            {
                foreach (Message message in chatMessageList)
                    message.SetSenderNameWidth(m_MessageSenderWidth);
                UpdateMessagePosition();
            }
        }

        internal void SetSenderFontSize(double fontSize)
        {
            m_MessageSenderFontSize = fontSize;
            if (m_ControlMessages.TryGetValue(m_CurrentChannel, out var chatMessageList))
            {
                foreach (Message message in chatMessageList)
                    message.SetSenderNameFontSize(m_MessageSenderFontSize);
                UpdateMessagePosition();
            }
        }

        internal void SetContentFontSize(double fontSize)
        {
            m_MessageContentFontSize = fontSize;
            if (m_ControlMessages.TryGetValue(m_CurrentChannel, out var chatMessageList))
            {
                foreach (Message message in chatMessageList)
                    message.SetMessageFontSize(m_MessageContentFontSize);
                UpdateMessagePosition();
            }
        }

        private void UpdateMessagePosition()
        {
            if (m_ControlMessages.TryGetValue(m_CurrentChannel, out var chatMessageList))
            {
                if (m_IsOnBottom)
                    System.Windows.Controls.DockPanel.SetDock(ChatPanel, Dock.Bottom);
                else
                    System.Windows.Controls.DockPanel.SetDock(ChatPanel, Dock.Top);
                double chatPanelHeight = 0;
                ChatPanel.Children.Clear();
                int i = (m_Reversed) ? chatMessageList.Count - 1 : 0;
                int last = (m_Reversed) ? -1 : chatMessageList.Count;
                int increment = (m_Reversed) ? -1 : 1;
                while (i != last)
                {
                    Message message = chatMessageList[i];
                    chatPanelHeight += message.ActualHeight;
                    ChatPanel.Children.Add(message);
                    i += increment;
                }
                ChatPanelDock.Height = chatPanelHeight;
            }
        }

        internal void ToggleHighlightedUser(string userName)
        {
            if (m_ChatHighlightedUsers.TryGetValue(m_CurrentChannel, out var highlightetUsers))
            {
                if (highlightetUsers.Contains(userName))
                    highlightetUsers.Remove(userName);
                else
                    highlightetUsers.Add(userName);
            }
        }

        private void OnChatMessage(UserMessage message)
        {
            Dispatcher.Invoke((Delegate)(() => {
                if (m_ControlMessages.TryGetValue(message.Channel, out var chatMessageList))
                {
                    Message chatMessage = new(this,
                        m_ChatPalette,
                        m_Translations,
                        message,
                        m_ChatHighlightedUsers[message.Channel].Contains(message.UserName),
                        m_MessageSenderWidth,
                        m_MessageSenderFontSize,
                        m_MessageContentFontSize);
                    m_Converter[chatMessage.MessageContent] = chatMessage;
                    chatMessage.MessageContent.Loaded += ChatMessage_Loaded;
                    ChatPanel.Children.Add(chatMessage);
                    chatMessageList.Add(chatMessage);
                }
            }));
        }

        private void UpdateScrollBar(bool extentHeightChange)
        {
            if (m_IsOnBottom == m_Reversed)
            {
                if (!extentHeightChange)
                    m_AutoScroll = (ChatScrollViewer.VerticalOffset == ChatScrollViewer.ScrollableHeight);
                if ((m_AutoScroll && extentHeightChange) || m_ForceAutoScroll)
                    ChatScrollViewer.ScrollToVerticalOffset(ChatScrollViewer.ExtentHeight);
            }
            else
            {
                if (!extentHeightChange)
                    m_AutoScroll = (ChatScrollViewer.VerticalOffset == 0);
                if ((m_AutoScroll && extentHeightChange) || m_ForceAutoScroll)
                    ChatScrollViewer.ScrollToVerticalOffset(0);
            }
        }

        private void ChatScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            UpdateScrollBar(e.ExtentHeightChange != 0);
        }

        private void ChatMessage_Loaded(object sender, RoutedEventArgs e)
        {
            if (m_Converter.TryGetValue((System.Windows.Controls.TextBox)sender, out var chatMessage))
            {
                chatMessage.UpdateEmotes();
                UpdateMessagePosition();
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ChatPanelDock.MinHeight = ChatScrollViewer.ActualHeight;
            ChatPanel.Width = e.NewSize.Width - 20;
            if (m_ControlMessages.TryGetValue(m_CurrentChannel, out var chatMessageList))
            {
                foreach (Message message in chatMessageList)
                    message.Width = ChatPanel.Width;
            }
            UpdateMessagePosition();
        }
    }
}
