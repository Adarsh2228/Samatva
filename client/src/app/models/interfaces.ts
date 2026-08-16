// ── Auth ────────────────────────────────────────────────────────────
export interface RegisterRequest {
  displayName: string;
  email: string;
  password: string;
  phoneNumber?: string;
  upiId?: string;
  defaultCurrency: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiry: string;
  user: UserDto;
}

export interface RefreshTokenRequest {
  accessToken: string;
  refreshToken: string;
}

export interface UserDto {
  id: string;
  displayName: string;
  email: string;
  phoneNumber?: string;
  avatarUrl?: string;
  upiId?: string;
  defaultCurrency: string;
  timeZone: string;
}

// ── Groups ──────────────────────────────────────────────────────────
export interface CreateGroupRequest {
  name: string;
  description?: string;
  defaultCurrency: string;
  imageUrl?: string;
}

export interface UpdateGroupRequest {
  name?: string;
  description?: string;
  defaultCurrency?: string;
  imageUrl?: string;
}

export interface GroupDto {
  id: string;
  name: string;
  description?: string;
  imageUrl?: string;
  defaultCurrency: string;
  isArchived: boolean;
  createdAt: string;
  members: GroupMemberDto[];
}

export interface GroupMemberDto {
  userId: string;
  displayName: string;
  email: string;
  avatarUrl?: string;
  role: string;
  joinedAt: string;
}

export interface AddMemberRequest {
  email: string;
  role: string;
}

export interface GuestLinkResponse {
  guestUrl: string;
  expiresAt: string;
}

// ── Expenses ────────────────────────────────────────────────────────
export type SplitType = 'Equal' | 'Exact' | 'Percentage' | 'Shares';
export type ExpenseCategory = 'General' | 'Food' | 'Transport' | 'Entertainment' | 'Shopping' | 'Utilities' | 'Rent' | 'Travel' | 'Healthcare' | 'Education' | 'Groceries' | 'Other';

export interface SplitDetailRequest {
  userId: string;
  value?: number;
}

export interface CreateExpenseRequest {
  groupId: string;
  description: string;
  amount: number;
  currency: string;
  category: ExpenseCategory;
  splitType: SplitType;
  expenseDate?: string;
  notes?: string;
  receiptUrl?: string;
  splits: SplitDetailRequest[];
}

export interface ExpenseDto {
  id: string;
  groupId: string;
  paidByUserId: string;
  paidByDisplayName: string;
  description: string;
  amount: number;
  currency: string;
  exchangeRate: number;
  category: string;
  splitType: string;
  expenseDate: string;
  receiptUrl?: string;
  notes?: string;
  isAiGenerated: boolean;
  createdAt: string;
  splits: ExpenseSplitDto[];
}

export interface ExpenseSplitDto {
  id: string;
  userId: string;
  userDisplayName: string;
  owedAmount: number;
  shareValue?: number;
  isSettled: boolean;
}

export interface PaginatedResponse<T> {
  data: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface BalanceDto {
  userId: string;
  displayName: string;
  netBalance: number;
  currency: string;
}

export interface DebtSimplificationDto {
  fromUserId: string;
  fromDisplayName: string;
  toUserId: string;
  toDisplayName: string;
  amount: number;
  currency: string;
}

// ── Settlements ─────────────────────────────────────────────────────
export interface CreateSettlementRequest {
  groupId: string;
  receiverUserId: string;
  amount: number;
  currency: string;
  notes?: string;
  paymentMethod?: string;
  upiTransactionId?: string;
}

export interface SettlementDto {
  id: string;
  groupId: string;
  payerUserId: string;
  payerDisplayName: string;
  receiverUserId: string;
  receiverDisplayName: string;
  amount: number;
  currency: string;
  status: string;
  settlementDate: string;
  notes?: string;
  paymentMethod?: string;
  upiTransactionId?: string;
  createdAt: string;
}

export interface UpiDeepLinkResponse {
  upiIntentUrl: string;
  payeeName: string;
  payeeUpiId: string;
  amount: number;
  transactionNote: string;
  qrCodeUrl: string;
}

// ── AI Assistant ────────────────────────────────────────────────────
export interface AiParseRequest {
  groupId: string;
  message: string;
}

export interface AiParseResponse {
  success: boolean;
  errorMessage?: string;
  parsedExpense?: ParsedExpenseData;
  rawInput: string;
  confidence: number;
}

export interface ParsedExpenseData {
  description: string;
  amount: number;
  currency: string;
  category: string;
  splitType: string;
  payerIdentifier?: string;
  participants: string[];
  splitDetails: ParsedSplitDetail[];
}

export interface ParsedSplitDetail {
  participantIdentifier: string;
  value: number;
}

// ── Chat Message (local UI model) ───────────────────────────────────
export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  parsedExpense?: ParsedExpenseData;
  createdExpense?: ExpenseDto;
  isLoading?: boolean;
}

// ── Spending Analytics ──────────────────────────────────────────────
export interface SpendingAnalyticsDto {
  totalSpent: number;
  totalOwed: number;
  totalOwedToYou: number;
  currency: string;
  categoryBreakdown: CategoryBreakdownDto[];
  dailySpending: DailySpendDto[];
  memberSpending: MemberSpendDto[];
  topCategory: string;
  averageExpense: number;
  totalTransactions: number;
}

export interface CategoryBreakdownDto {
  category: string;
  amount: number;
  count: number;
  percentage: number;
}

export interface DailySpendDto {
  date: string;
  amount: number;
  count: number;
}

export interface MemberSpendDto {
  userId: string;
  displayName: string;
  totalPaid: number;
  totalOwed: number;
  netBalance: number;
}

// ── Settlement Breakdown ─────────────────────────────────────────────
export interface SettlementBreakdownItem {
  expenseId: string;
  description: string;
  totalAmount: number;
  yourShare: number;
  currency: string;
  paidBy: string;
  date: string;
  category: string;
}

export interface SettlementBreakdownResponse {
  receiverUserId: string;
  receiverName: string;
  receiverUpiId?: string;
  receiverPhone?: string;
  totalOwed: number;
  currency: string;
  breakdown: SettlementBreakdownItem[];
  upiIntentUrl?: string;
  qrCodeUrl?: string;
  whatsAppUrl?: string;
}
