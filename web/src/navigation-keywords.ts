/**
 * Keyword-first navigation vocabulary for RBOS.
 *
 * Each profile is a small set of required semantic groups. A command needs
 * evidence from every group before it can navigate; a single generic word
 * such as "return", "payment" or "stock" is handled as an ambiguity instead.
 *
 * Keep these as words/concepts, not whole-sentence examples. Add new dialect
 * spellings here only when they are understood and reviewed by a speaker.
 */
export type KeywordProfile = {
  /** Every group must have at least one matching token. */
  readonly groups: readonly (readonly string[])[]
}

export const navigationKeywordProfiles: Record<string, KeywordProfile> = {
  home: { groups: [['home', 'dashboard', 'main', 'start', 'ghar']] },
  'sell.menu': { groups: [['sell'], ['menu', 'option', 'section']] },
  'sell.bill': { groups: [['bill'], ['new', 'create', 'make', 'generate']] },
  'sell.bills': { groups: [['bill'], ['old', 'history', 'list', 'past', 'previous']] },
  'sell.collect': { groups: [['customer'], ['collect', 'receive', 'payment', 'take', 'vasooli']] },
  'sell.return': { groups: [['customer', 'sell'], ['return']] },
  'buy.receive': { groups: [['purchase', 'supplier'], ['new', 'add', 'receive', 'entry', 'arrival', 'goods']] },
  'buy.pay': { groups: [['supplier'], ['payment', 'pay', 'settle', 'chukana']] },
  'buy.history': { groups: [['purchase', 'supplier'], ['history', 'old', 'list', 'past']] },
  'buy.return': { groups: [['supplier', 'purchase'], ['return']] },
  'stock.current': { groups: [['stock', 'goods'], ['current', 'view', 'available', 'remaining', 'bacha']] },
  'stock.products': { groups: [['product', 'item', 'catalogue'], ['list', 'add', 'manage', 'new']] },
  'stock.opening': { groups: [['opening', 'starting', 'initial', 'shuru'], ['stock', 'goods']] },
  'stock.import': { groups: [['import', 'upload', 'excel', 'csv', 'bulk', 'file', 'setup']] },
  'stock.adjust': { groups: [['adjust', 'correction', 'correct', 'change', 'damage', 'broken'], ['stock', 'goods', 'quantity']] },
  'stock.count': { groups: [['count', 'ginti', 'physical', 'verify'], ['stock', 'goods', 'item']] },
  'stock.history': { groups: [['stock', 'adjustment', 'correction'], ['history', 'record', 'past']] },
  'stock.alerts': { groups: [['low', 'shortage', 'alert', 'ageing', 'expiry', 'slow'], ['stock', 'goods', 'product', 'item']] },
  'stock.units': { groups: [['unit', 'measure', 'conversion', 'box', 'piece', 'kg', 'litre', 'dozen']] },
  'parties.customers': { groups: [['customer']] },
  'parties.suppliers': { groups: [['supplier']] },
  'parties.purchases': { groups: [['customer'], ['purchase', 'bought', 'history', 'kharid']] },
  'parties.dues': { groups: [['customer'], ['dues', 'outstanding', 'baki', 'udhar', 'ageing']] },
  'money.cash': { groups: [['cash', 'bank', 'galla', 'cashbook']] },
  'money.expenses': { groups: [['expense', 'kharcha', 'spending']] },
  'money.owner': { groups: [['owner', 'malik'], ['capital', 'drawing', 'withdrawal', 'deposit']] },
  'money.closing': { groups: [['day', 'daily', 'closing', 'end'], ['hisab', 'account', 'cash', 'galla']] },
  'more.menu': { groups: [['more', 'other', 'baaki'], ['menu', 'option']] },
  'more.reports': { groups: [['report', 'profit', 'loss', 'pnl']] },
  'more.print': { groups: [['print', 'printing', 'printer', 'chhapai']] },
  'more.lock': { groups: [['lock', 'freeze'], ['period', 'month', 'books', 'hisab']] },
  'more.audit': { groups: [['audit', 'change', 'log'], ['history', 'record', 'trail']] },
  'more.gst': { groups: [['gst', 'tax']] },
  'more.staff': { groups: [['staff', 'employee', 'team', 'karmchari']] },
  'more.branches': { groups: [['branch', 'branches', 'shoplocation']] },
  'more.transfers': { groups: [['transfer', 'move'], ['stock', 'goods']] },
  'more.orders': { groups: [['order', 'po'], ['purchase', 'supplier', 'goods', 'mangwana']] },
  'more.reconcile': { groups: [['reconcile', 'match', 'milan'], ['bank', 'statement', 'passbook']] },
  'more.loans': { groups: [['loan', 'karza', 'borrow', 'borrowing']] },
  settings: { groups: [['setting', 'settings', 'profile', 'business', 'shop']] },
}

/**
 * Word-level normalisation aliases. Keep this list explainable and conservative:
 * ambiguous words are not forced to one screen; they are resolved by context.
 */
export const navigationWordAliases: Record<string, string> = {
  // Home and navigation
  'होम': 'home', 'घर': 'ghar', 'मुख्य': 'main', 'डैशबोर्ड': 'dashboard',
  'बिक्री': 'sell', 'बिकरी': 'sell', 'बेच': 'sell', 'बेचना': 'sell', 'सेल': 'sell', 'sales': 'sell', 'selling': 'sell', 'bikri': 'sell', 'bikree': 'sell', 'vikri': 'sell', 'bech': 'sell', 'bechna': 'sell',
  'मेनू': 'menu', 'मेन्यू': 'menu', 'विकल्प': 'option', 'options': 'option', 'section': 'section',
  // Bills and receipts
  'बिल': 'bill', 'बील': 'bill', 'बिलो': 'bill', 'बिलिंग': 'bill', 'चालान': 'bill', 'इनवॉइस': 'bill', 'रसीद': 'bill', 'पर्ची': 'bill', 'पर्चा': 'bill',
  'bil': 'bill', 'bll': 'bill', 'bila': 'bill', 'invoice': 'bill', 'challan': 'bill', 'parchi': 'bill', 'parcha': 'bill', 'receipt': 'bill', 'rasid': 'bill',
  // Customers and suppliers
  'ग्राहक': 'customer', 'ग्राहकों': 'customer', 'ग्रहक': 'customer', 'कस्टमर': 'customer', 'ग्राहकी': 'customer',
  'grahak': 'customer', 'graahak': 'customer', 'grahakko': 'customer', 'grahako': 'customer', 'customer': 'customer', 'customers': 'customer', 'custmer': 'customer', 'costumer': 'customer',
  'सप्लायर': 'supplier', 'सप्लायरों': 'supplier', 'सप्लियर': 'supplier', 'विक्रेता': 'supplier', 'व्यापारी': 'supplier', 'डीलर': 'supplier', 'वेंडर': 'supplier',
  'supplier': 'supplier', 'suppliers': 'supplier', 'suplier': 'supplier', 'saplayer': 'supplier', 'vendor': 'supplier', 'dealer': 'supplier', 'wholesaler': 'supplier', 'vyapari': 'supplier',
  // Goods and stock
  'स्टॉक': 'stock', 'स्टोक': 'stock', 'इन्वेंटरी': 'stock', 'भंडार': 'stock',
  'stock': 'stock', 'stok': 'stock', 'stokh': 'stock', 'inventory': 'stock',
  'माल': 'goods', 'मालो': 'goods', 'सामान': 'goods', 'सामान': 'goods', 'वस्तु': 'goods',
  'maal': 'goods', 'mal': 'goods', 'saman': 'goods', 'samaan': 'goods', 'item': 'item', 'items': 'item', 'product': 'product', 'products': 'product', 'प्रोडक्ट': 'product', 'उत्पाद': 'product', 'कैटलॉग': 'catalogue', 'catalogue': 'catalogue', 'catalog': 'catalogue',
  // Purchase, receipt, and supplier workflows
  'खरीद': 'purchase', 'खरीदी': 'purchase', 'खरीदारी': 'purchase', 'परचेज': 'purchase', 'मंगाई': 'purchase',
  'kharid': 'purchase', 'kharidi': 'purchase', 'khareed': 'purchase', 'purchase': 'purchase', 'purches': 'purchase', 'procurement': 'purchase',
  'आया': 'arrival', 'आई': 'arrival', 'आए': 'arrival', 'आयो': 'arrival', 'उतरा': 'arrival', 'उतरी': 'arrival', 'aaya': 'arrival', 'aayi': 'arrival', 'aaye': 'arrival', 'aayo': 'arrival', 'utra': 'arrival', 'utara': 'arrival', 'arrival': 'arrival', 'receive': 'receive', 'received': 'receive', 'मिला': 'receive',
  // Payments, dues, and returns
  'भुगतान': 'payment', 'पेमेंट': 'payment', 'भुगतान': 'payment', 'रकम': 'payment', 'payment': 'payment', 'pymnt': 'payment', 'pay': 'pay',
  'उधार': 'udhar', 'उधारी': 'udhar', 'उधारि': 'udhar', 'बकाया': 'baki', 'बाकी': 'baki', 'बकाया': 'baki',
  'udhar': 'udhar', 'udhaar': 'udhar', 'baki': 'baki', 'baaki': 'baki', 'bakaya': 'baki', 'dues': 'dues', 'outstanding': 'outstanding', 'vasooli': 'vasooli', 'वसूली': 'vasooli',
  'वापसी': 'return', 'वापस': 'return', 'लौट': 'return', 'लौटाना': 'return', 'लौटाया': 'return', 'return': 'return', 'wapas': 'return', 'wapasi': 'return', 'lautana': 'return', 'lauta': 'return', 'lautaya': 'return',
  // Common navigation intent and time modifiers
  'नया': 'new', 'नई': 'new', 'नए': 'new', 'नयी': 'new', 'बनाओ': 'new', 'बनाना': 'new', 'बना': 'new', 'बनाइए': 'new',
  'naya': 'new', 'nayi': 'new', 'naye': 'new', 'banao': 'new', 'bnao': 'new', 'bana': 'new', 'banado': 'new', 'create': 'new', 'make': 'new', 'generate': 'new', 'add': 'add', 'जोड़ो': 'add', 'जोड़ना': 'add', 'जोड़': 'add', 'jodo': 'add', 'jodna': 'add',
  'पुराना': 'old', 'पुराने': 'old', 'पिछला': 'old', 'पिछली': 'old', 'पिछले': 'old', 'पहले': 'old', 'old': 'old', 'past': 'past', 'previous': 'old', 'pichla': 'old', 'pichle': 'old', 'pichhle': 'old', 'purana': 'old', 'purane': 'old',
  'इतिहास': 'history', 'हिस्ट्री': 'history', 'history': 'history', 'record': 'record', 'रिकॉर्ड': 'record', 'सूची': 'list', 'लिस्ट': 'list', 'list': 'list',
  'दिखाओ': 'view', 'दिखाइए': 'view', 'दिखाना': 'view', 'देखो': 'view', 'देखना': 'view', 'खोलो': 'view', 'खोलिए': 'view', 'खोलना': 'view',
  'dikhao': 'view', 'dikha': 'view', 'dikhao': 'view', 'dekho': 'view', 'dekhna': 'view', 'khol': 'view', 'kholo': 'view', 'khul': 'view', 'show': 'view', 'open': 'view', 'view': 'view', 'check': 'view', 'manage': 'manage',
  // Stock maintenance and measurements
  'समायोजन': 'adjust', 'सुधार': 'correction', 'ठीक': 'correct', 'गिनती': 'ginti', 'गिनना': 'count', 'गिनो': 'count',
  'adjust': 'adjust', 'adjustment': 'adjustment', 'correction': 'correction', 'correct': 'correct', 'change': 'change', 'damage': 'damage', 'broken': 'damage', 'toota': 'damage', 'count': 'count', 'ginti': 'ginti', 'ginati': 'ginti', 'physical': 'physical', 'verify': 'verify',
  'कम': 'low', 'कमतर': 'low', 'लो': 'low', 'अलर्ट': 'alert', 'चेतावनी': 'alert', 'समाप्त': 'expiry',
  'low': 'low', 'shortage': 'shortage', 'alert': 'alert', 'alerts': 'alert', 'ageing': 'ageing', 'aging': 'ageing', 'expiry': 'expiry', 'expired': 'expiry', 'slow': 'slow',
  'यूनिट': 'unit', 'इकाई': 'unit', 'नाप': 'measure', 'माप': 'measure', 'बॉक्स': 'box', 'पीस': 'piece', 'किलो': 'kg', 'किलोग्राम': 'kg', 'लीटर': 'litre',
  'unit': 'unit', 'units': 'unit', 'measure': 'measure', 'conversion': 'conversion', 'box': 'box', 'piece': 'piece', 'pieces': 'piece', 'kg': 'kg', 'kilo': 'kg', 'kilogram': 'kg', 'litre': 'litre', 'liter': 'litre', 'dozen': 'dozen',
  // Finance
  'नकद': 'cash', 'रोकड़': 'cash', 'गल्ला': 'galla', 'बैंक': 'bank', 'खाता': 'account', 'बही': 'account', 'हिसाब': 'hisab',
  'cash': 'cash', 'bank': 'bank', 'galla': 'galla', 'cashbook': 'cashbook', 'cashbook': 'cashbook', 'paisa': 'money', 'paise': 'money', 'money': 'money', 'account': 'account', 'hisab': 'hisab',
  'खर्च': 'expense', 'खर्चा': 'expense', 'खरचा': 'expense', 'व्यय': 'expense', 'expense': 'expense', 'expenses': 'expense', 'expence': 'expense', 'kharch': 'kharcha', 'kharcha': 'kharcha', 'spending': 'spending',
  'मालिक': 'malik', 'मालिकाना': 'malik', 'पूंजी': 'capital', 'निवेश': 'capital', 'निकासी': 'withdrawal', 'निकाला': 'withdrawal',
  'owner': 'owner', 'malik': 'malik', 'capital': 'capital', 'drawing': 'drawing', 'drawings': 'drawing', 'withdrawal': 'withdrawal', 'withdraw': 'withdrawal', 'deposit': 'deposit',
  'दिन': 'day', 'रोज': 'daily', 'दैनिक': 'daily', 'समापन': 'closing', 'बंद': 'closing', 'day': 'day', 'daily': 'daily', 'closing': 'closing', 'end': 'end',
  // More and administration
  'और': 'more', 'बाकी': 'baaki', 'अन्य': 'other', 'more': 'more', 'other': 'other', 'option': 'option',
  'रिपोर्ट': 'report', 'रिपोर्टें': 'report', 'report': 'report', 'reports': 'report', 'profit': 'profit', 'लाभ': 'profit', 'loss': 'loss', 'हानि': 'loss', 'pnl': 'pnl',
  'प्रिंट': 'print', 'छापना': 'print', 'छपाई': 'chhapai', 'print': 'print', 'printing': 'print', 'printer': 'printer', 'chhapai': 'chhapai',
  'लॉक': 'lock', 'ताला': 'lock', 'lock': 'lock', 'freeze': 'freeze', 'अवधि': 'period', 'महीना': 'month', 'माह': 'month', 'period': 'period', 'month': 'month', 'books': 'books',
  'ऑडिट': 'audit', 'बदलाव': 'change', 'audit': 'audit', 'change': 'change', 'log': 'log', 'trail': 'trail',
  'जीएसटी': 'gst', 'gst': 'gst', 'कर': 'tax', 'tax': 'tax',
  'कर्मचारी': 'employee', 'स्टाफ': 'staff', 'नौकर': 'employee', 'staff': 'staff', 'employee': 'employee', 'team': 'team', 'karmchari': 'employee',
  'शाखा': 'branch', 'शाखाएं': 'branch', 'ब्रांच': 'branch', 'branch': 'branch', 'branches': 'branch', 'shoplocation': 'shoplocation',
  'ट्रांसफर': 'transfer', 'स्थानांतरण': 'transfer', 'transfer': 'transfer', 'move': 'move',
  'ऑर्डर': 'order', 'आर्डर': 'order', 'मंगवाना': 'mangwana', 'मंगाना': 'mangwana', 'order': 'order', 'orders': 'order', 'po': 'po', 'mangwana': 'mangwana',
  'मिलान': 'milan', 'मिलाना': 'milan', 'reconcile': 'reconcile', 'match': 'match', 'milan': 'milan', 'statement': 'statement', 'passbook': 'passbook',
  'लोन': 'loan', 'कर्ज': 'karza', 'कर्जा': 'karza', 'ऋण': 'loan', 'loan': 'loan', 'loans': 'loan', 'karza': 'karza', 'borrow': 'borrow', 'borrowing': 'borrow',
  'सेटिंग': 'setting', 'सेटिंग्स': 'setting', 'जानकारी': 'details', 'दुकान': 'shop', 'setting': 'setting', 'settings': 'setting', 'profile': 'profile', 'business': 'business', 'shop': 'shop', 'dukaan': 'shop', 'details': 'details',
  // Bundeli / conversational particles and inflections
  'moro': 'my', 'mor': 'my', 'mori': 'my', 'मोर': 'my', 'मोरो': 'my', 'मोरी': 'my',
  'mai': 'i', 'main': 'i', 'mae': 'i', 'ham': 'we', 'hama': 'we', 'हम': 'we', 'मैं': 'i',
  'करे': 'do', 'करो': 'do', 'करना': 'do', 'करिये': 'do', 'करिए': 'do',
}

export const navigationStopWords = new Set([
  'please', 'plz', 'the', 'a', 'an', 'to', 'me', 'my', 'i', 'we', 'for', 'with', 'from', 'of',
  'karo', 'kar', 'kr', 'krna', 'kro', 'kijiye', 'kiji', 'kiye', 'do', 'ji', 'bhai', 'bhaiya', 'bhayya',
  'kholo', 'kholiye', 'dikhao', 'dikha', 'dikhaao', 'dekho', 'dekhna', 'jao', 'chalo', 'par', 'pe',
  'mujhe', 'mujko', 'zara', 'ab', 'ek', 'aur', 'se', 'ka', 'ki', 'ke', 'ko', 'hai', 'hain', 'ho',
  'mera', 'meri', 'mere', 'apna', 'apne', 'apni', 'wala', 'wali', 'wale', 'ji', 'aap', 'tum',
  'है', 'हैं', 'था', 'थी', 'थे', 'को', 'का', 'की', 'के', 'से', 'में', 'पर', 'मुझे', 'मेरे', 'मेरी',
  'और', 'एक', 'अब', 'जी', 'कृपया', 'करो', 'करना', 'करिए', 'करिये', 'दिखाओ', 'खोलो',
])
