//auto scroll of the chat window to the newest message (called from Chat.razor)
export function scrollToBottom(elementId) {
    const el = document.getElementById(elementId);
    if (el) el.scrollTop = el.scrollHeight;
}
