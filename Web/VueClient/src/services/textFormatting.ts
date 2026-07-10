export function formatPassiveDescription(text: string): string {
  const escaped = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')

  return escaped
    .replace(/__\*\*\*(.+?)\*\*\*__/gs, '<u><strong><em>$1</em></strong></u>')
    .replace(/__\*\*(.+?)\*\*__/gs, '<u><strong>$1</strong></u>')
    .replace(/\*\*\*(.+?)\*\*\*/gs, '<strong><em>$1</em></strong>')
    .replace(/\*\*(.+?)\*\*/gs, '<strong>$1</strong>')
    .replace(/__(.+?)__/gs, '<u>$1</u>')
    .replace(/\*(.+?)\*/gs, '<em>$1</em>')
    .replace(/\n/g, '<br>')
}
