export function isStanEdgarThresholdDialogue(text: string): boolean {
  return /^Stan Edgar:\s*["«“]/u.test(text)
    && /(?:70\s+очков|70\s+points)/iu.test(text)
}

type LogEntry = {
  raw: string
  isPhrase: boolean
}

function isNoirLine(entry: LogEntry): boolean {
  return entry.isPhrase
    && /^Stan Edgar:/u.test(entry.raw)
    && /(?:Нуар|Noir)/iu.test(entry.raw)
}

function isHomelanderReply(entry: LogEntry): boolean {
  return /^Homelander:/u.test(entry.raw)
    && /(?:сверхчеловек|superhuman)/iu.test(entry.raw)
}

function isDefectiveProductLine(entry: LogEntry): boolean {
  return /^Stan Edgar:/u.test(entry.raw)
    && /(?:бракованный продукт|defective product)/iu.test(entry.raw)
}

function isMissileVolleyLine(entry: LogEntry): boolean {
  return /^(?:Залп V-наводящихся ракет|A volley of V-seeking missiles)/iu.test(entry.raw)
}

/**
 * The server records Homelander's private Noir phrase after the four public dismissal lines,
 * while the client normally presents personal entries before global ones. Move only that
 * private phrase back behind its complete public sequence.
 */
export function orderStanEdgarDismissalLogs<T extends LogEntry>(entries: T[]): T[] {
  const noirIndex = entries.findIndex(isNoirLine)
  const thresholdIndex = entries.findIndex(entry => isStanEdgarThresholdDialogue(entry.raw))
  const homelanderIndex = entries.findIndex((entry, index) =>
    index > thresholdIndex && isHomelanderReply(entry))
  const productIndex = entries.findIndex((entry, index) =>
    index > homelanderIndex && isDefectiveProductLine(entry))
  const volleyIndex = entries.findIndex((entry, index) =>
    index > productIndex && isMissileVolleyLine(entry))

  const hasCompletePublicSequence = thresholdIndex >= 0
    && homelanderIndex > thresholdIndex
    && productIndex > homelanderIndex
    && volleyIndex > productIndex
  if (!hasCompletePublicSequence || noirIndex < 0 || noirIndex > volleyIndex) return entries

  const noirEntry = entries[noirIndex]
  const volleyEntry = entries[volleyIndex]
  const ordered = entries.filter((_entry, index) => index !== noirIndex)
  const orderedVolleyIndex = ordered.indexOf(volleyEntry)
  ordered.splice(orderedVolleyIndex + 1, 0, noirEntry)
  return ordered
}
