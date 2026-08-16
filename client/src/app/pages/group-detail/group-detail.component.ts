import { Component, signal, OnInit, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { GroupService } from '../../services/group.service';
import { ExpenseService } from '../../services/expense.service';
import { ThemeService } from '../../services/theme.service';
import { GroupDto, ExpenseDto, BalanceDto, DebtSimplificationDto, CreateExpenseRequest, ChatMessage, UpiDeepLinkResponse, SpendingAnalyticsDto, SettlementBreakdownResponse } from '../../models/interfaces';

@Component({
  selector: 'app-group-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DatePipe],
  templateUrl: './group-detail.component.html',
  styleUrl: './group-detail.component.scss'
})
export class GroupDetailComponent implements OnInit {
  private auth = inject(AuthService);
  private groupService = inject(GroupService);
  private expenseService = inject(ExpenseService);
  private theme = inject(ThemeService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  user = this.auth.user;
  isDark = this.theme.isDark;
  groupId = '';
  group = signal<GroupDto | null>(null);
  expenses = signal<ExpenseDto[]>([]);
  balances = signal<BalanceDto[]>([]);
  debts = signal<DebtSimplificationDto[]>([]);
  isLoading = signal(true);
  activeTab = signal<'expenses' | 'balances' | 'settle' | 'insights'>('expenses');

  // Add Expense
  showAddExpense = signal(false);
  newExpense: any = { description: '', amount: null, currency: 'INR', category: 'General', splitType: 'Equal', notes: '' };
  splitValues: Record<string, number> = {};

  // AI Chat
  showAiChat = signal(false);
  chatMessages = signal<ChatMessage[]>([]);
  chatInput = '';
  isChatLoading = signal(false);

  // ── Invite Members (Multi-method) ────────────────────────────
  showInviteModal = signal(false);
  inviteTab = signal<'link' | 'email' | 'qr' | 'code'>('link');
  inviteData = signal<any>(null);
  inviteLoading = signal(false);
  inviteEmail = '';
  inviteMsg = signal('');
  inviteMsgType = signal<'success' | 'error'>('success');
  linkCopied = signal(false);

  // ── Invite Code ──────────────────────────────────────────────
  inviteCode = signal('');
  inviteCodeExpiry = signal('');
  joinCode = '';
  codeCopied = signal(false);

  // ── UPI Payment Modal ────────────────────────────────────────
  showUpiModal = signal(false);
  upiData = signal<UpiDeepLinkResponse | null>(null);
  upiLoading = signal(false);
  upiSettlementDebt = signal<DebtSimplificationDto | null>(null);
  isMobileDevice = signal(false);
  upiPaymentRecorded = signal(false);
  upiTxnId = '';

  // ── Advanced Settlement Modal ─────────────────────────────
  showSettleModal = signal(false);
  settleBreakdown = signal<SettlementBreakdownResponse | null>(null);
  settleLoading = signal(false);
  activePayTab = signal<'upi' | 'qr' | 'whatsapp'>('upi');
  qrLoaded = signal(false);

  // ── Spending Analytics ───────────────────────────────────────
  analytics = signal<SpendingAnalyticsDto | null>(null);
  analyticsLoading = signal(false);

  // ── Expense Streak & Gamification ────────────────────────────
  currentStreak = signal(0);
  longestStreak = signal(0);
  streakMessage = signal('');
  totalGroupSaved = signal(0);

  ngOnInit() {
    if (!this.auth.isAuthenticated()) { this.router.navigate(['/login']); return; }
    this.groupId = this.route.snapshot.paramMap.get('id') || '';
    this.isMobileDevice.set(/Android|iPhone|iPad|iPod/i.test(navigator.userAgent));
    this.loadAll();
  }

  loadAll() {
    this.isLoading.set(true);
    this.groupService.getGroup(this.groupId).subscribe({ next: g => this.group.set(g), error: () => this.router.navigate(['/dashboard']) });
    this.expenseService.getGroupExpenses(this.groupId).subscribe({
      next: r => { this.expenses.set(r.data); this.isLoading.set(false); this.calculateStreak(r.data); },
      error: () => this.isLoading.set(false)
    });
    this.expenseService.getGroupBalances(this.groupId).subscribe({ next: b => this.balances.set(b) });
    this.expenseService.getSimplifiedDebts(this.groupId).subscribe({ next: d => this.debts.set(d) });
  }

  // ── Expense Ownership Check ──────────────────────────────────
  isMyExpense(exp: ExpenseDto): boolean {
    return exp.paidByUserId === this.user()?.id;
  }

  addExpense() {
    const splits = this.buildSplits();
    const req: CreateExpenseRequest = {
      groupId: this.groupId, description: this.newExpense.description,
      amount: this.newExpense.amount, currency: this.newExpense.currency || 'INR',
      category: this.newExpense.category || 'General', splitType: this.newExpense.splitType || 'Equal',
      notes: this.newExpense.notes, splits
    };
    this.expenseService.createExpense(req).subscribe({
      next: (e) => {
        this.expenses.update(es => [e, ...es]);
        this.showAddExpense.set(false);
        this.newExpense = { description: '', amount: null, currency: 'INR', category: 'General', splitType: 'Equal', notes: '' };
        this.splitValues = {};
        this.loadBalances();
      }
    });
  }

  // ── Split Helpers ──────────────────────────────────────────────
  initSplitValues() {
    this.splitValues = {};
    const members = this.group()?.members || [];
    members.forEach(m => { this.splitValues[m.userId] = 0; });
  }

  onAmountChange() { /* triggered by amount input for reactivity */ }

  buildSplits(): any[] {
    if (this.newExpense.splitType === 'Equal') return [];
    return Object.entries(this.splitValues)
      .filter(([_, v]) => v > 0)
      .map(([userId, value]) => ({ userId, value }));
  }

  getSplitTotal(): number {
    return Object.values(this.splitValues).reduce((sum, v) => sum + (v || 0), 0);
  }

  getPercentTotal(): number {
    return Object.values(this.splitValues).reduce((sum, v) => sum + (v || 0), 0);
  }

  getShareTotal(): number {
    return Object.values(this.splitValues).reduce((sum, v) => sum + (v || 0), 0);
  }

  isSplitValid(): boolean {
    const st = this.newExpense.splitType;
    if (st === 'Equal') return true;
    if (st === 'Exact') return Math.abs(this.getSplitTotal() - (this.newExpense.amount || 0)) < 0.01;
    if (st === 'Percentage') return Math.abs(this.getPercentTotal() - 100) < 0.01;
    if (st === 'Shares') return this.getShareTotal() > 0;
    return true;
  }

  canSubmitExpense(): boolean {
    return !!this.newExpense.description && !!this.newExpense.amount && this.newExpense.amount > 0 && this.isSplitValid();
  }

  deleteExpense(id: string) {
    // Only the creator can delete (backend enforces too)
    this.expenseService.deleteExpense(id).subscribe({
      next: () => { this.expenses.update(es => es.filter(e => e.id !== id)); this.loadBalances(); }
    });
  }

  loadBalances() {
    this.expenseService.getGroupBalances(this.groupId).subscribe({ next: b => this.balances.set(b) });
    this.expenseService.getSimplifiedDebts(this.groupId).subscribe({ next: d => this.debts.set(d) });
  }

  // ── Multi-Method Invite System ───────────────────────────────
  openInviteModal() {
    this.showInviteModal.set(true);
    this.inviteMsg.set('');
    this.linkCopied.set(false);
    this.generateInviteLink();
  }

  generateInviteLink() {
    this.inviteLoading.set(true);
    this.groupService.generateInviteLink(this.groupId).subscribe({
      next: (data) => { this.inviteData.set(data); this.inviteLoading.set(false); },
      error: () => this.inviteLoading.set(false)
    });
  }

  copyInviteLink() {
    const url = this.inviteData()?.inviteUrl;
    if (url) {
      navigator.clipboard.writeText(url).then(() => {
        this.linkCopied.set(true);
        setTimeout(() => this.linkCopied.set(false), 3000);
      });
    }
  }

  shareViaWhatsApp() {
    const url = this.inviteData()?.whatsappUrl;
    if (url) window.open(url, '_blank');
  }

  shareViaSMS() {
    const body = this.inviteData()?.smsBody;
    if (body) window.open(`sms:?body=${encodeURIComponent(body)}`, '_self');
  }

  shareViaEmail() {
    const data = this.inviteData();
    if (data) {
      window.open(`mailto:?subject=${encodeURIComponent(data.emailSubject)}&body=${encodeURIComponent(data.emailBody)}`, '_self');
    }
  }

  shareViaTelegram() {
    const url = this.inviteData()?.inviteUrl;
    const text = `Join my group "${this.group()?.name}" on Samatva! ⚖️`;
    if (url) window.open(`https://t.me/share/url?url=${encodeURIComponent(url)}&text=${encodeURIComponent(text)}`, '_blank');
  }

  nativeShare() {
    const data = this.inviteData();
    if (data && navigator.share) {
      navigator.share({
        title: `Join "${this.group()?.name}" on Samatva`,
        text: `Join my expense group "${this.group()?.name}"!`,
        url: data.inviteUrl
      });
    }
  }

  addMemberByEmail() {
    if (!this.inviteEmail.trim()) return;
    this.inviteLoading.set(true);
    this.groupService.addMember(this.groupId, { email: this.inviteEmail, role: 'Member' }).subscribe({
      next: (m) => {
        this.group.update(g => g ? { ...g, members: [...g.members, m] } : g);
        this.inviteMsg.set(`✅ ${m.displayName} added to the group!`);
        this.inviteMsgType.set('success');
        this.inviteEmail = '';
        this.inviteLoading.set(false);
      },
      error: (err) => {
        this.inviteMsg.set(err.error?.message || '❌ Failed to add member. Share the invite link instead!');
        this.inviteMsgType.set('error');
        this.inviteLoading.set(false);
      }
    });
  }

  // ── Invite Code ──────────────────────────────────────────────
  generateCode() {
    this.inviteLoading.set(true);
    this.groupService.generateInviteCode(this.groupId).subscribe({
      next: (data) => {
        this.inviteCode.set(data.code);
        this.inviteCodeExpiry.set(data.expiresAt);
        this.inviteLoading.set(false);
      },
      error: () => this.inviteLoading.set(false)
    });
  }

  copyCode() {
    navigator.clipboard.writeText(this.inviteCode()).then(() => {
      this.codeCopied.set(true);
      setTimeout(() => this.codeCopied.set(false), 3000);
    });
  }

  // ── Nudge / Remind ────────────────────────────────────────────
  nudgeUser(debt: DebtSimplificationDto) {
    const msg = `Hey ${debt.fromDisplayName}! You owe ${this.getCurrencySymbol(debt.currency)}${debt.amount} to ${debt.toDisplayName} in "${this.group()?.name}". Settle up on Samatva! ⚖️`;
    if (navigator.share) {
      navigator.share({ title: 'Samatva Reminder', text: msg });
    } else {
      const whatsappUrl = `https://wa.me/?text=${encodeURIComponent(msg)}`;
      window.open(whatsappUrl, '_blank');
    }
  }

  // ── UPI Settlement (Enhanced with QR + Deep Link) ─────────────
  settleViaUpi(debt: DebtSimplificationDto) {
    this.upiSettlementDebt.set(debt);
    this.upiLoading.set(true);
    this.upiPaymentRecorded.set(false);
    this.upiTxnId = '';
    this.showUpiModal.set(true);
    this.expenseService.getUpiLink(debt.toUserId, debt.amount, this.groupId).subscribe({
      next: (upi) => { this.upiData.set(upi); this.upiLoading.set(false); },
      error: () => { this.upiLoading.set(false); this.showUpiModal.set(false); alert('UPI ID not set for this user. Ask them to update their profile!'); }
    });
  }

  openUpiApp() {
    const url = this.upiData()?.upiIntentUrl;
    if (url) window.location.href = url;
  }

  recordUpiPayment() {
    const debt = this.upiSettlementDebt();
    if (!debt) return;
    this.expenseService.createSettlement({
      groupId: this.groupId, receiverUserId: debt.toUserId,
      amount: debt.amount, currency: debt.currency,
      paymentMethod: 'UPI', upiTransactionId: this.upiTxnId || undefined,
      notes: `Paid via UPI to ${debt.toDisplayName}`
    }).subscribe({
      next: () => { this.upiPaymentRecorded.set(true); this.loadBalances(); },
      error: () => alert('Failed to record payment.')
    });
  }

  closeUpiModal() {
    this.showUpiModal.set(false);
    this.upiData.set(null);
    this.upiSettlementDebt.set(null);
  }

  // ── Advanced Settlement Modal ─────────────────────────────
  openSettleModal(debt: DebtSimplificationDto): void {
    this.showSettleModal.set(true);
    this.settleBreakdown.set(null);
    this.settleLoading.set(true);
    this.activePayTab.set('upi');
    this.qrLoaded.set(false);
    this.expenseService.getSettlementBreakdown(this.groupId, debt.toUserId).subscribe({
      next: (data) => { this.settleBreakdown.set(data); this.settleLoading.set(false); },
      error: () => this.settleLoading.set(false)
    });
  }

  copyUpiId(): void {
    const upiId = this.settleBreakdown()?.receiverUpiId;
    if (upiId) navigator.clipboard.writeText(upiId);
  }

  openUpiIntentApp(): void {
    const url = this.settleBreakdown()?.upiIntentUrl;
    if (url) window.open(url, '_blank');
  }

  openWhatsApp(): void {
    const url = this.settleBreakdown()?.whatsAppUrl;
    if (url) window.open(url, '_blank');
  }

  // ── Spending Analytics ────────────────────────────────────────
  loadAnalytics() {
    if (this.analytics()) return;
    this.analyticsLoading.set(true);
    this.expenseService.getSpendingAnalytics(this.groupId).subscribe({
      next: (a) => { this.analytics.set(a); this.analyticsLoading.set(false); },
      error: () => this.analyticsLoading.set(false)
    });
  }

  onTabChange(tab: 'expenses' | 'balances' | 'settle' | 'insights') {
    this.activeTab.set(tab);
    if (tab === 'insights') this.loadAnalytics();
  }

  getCategoryColor(cat: string): string {
    const map: Record<string, string> = {
      Food: '#FF6B6B', Transport: '#74B9FF', Entertainment: '#A29BFE',
      Shopping: '#FDCB6E', Utilities: '#55EFC4', Rent: '#E17055',
      Travel: '#00CEC9', Healthcare: '#FF7675', Education: '#6C5CE7',
      Groceries: '#00B894', General: '#636E72', Other: '#B2BEC3'
    };
    return map[cat] || '#636E72';
  }

  getMaxDailySpend(): number {
    const a = this.analytics();
    if (!a) return 1;
    return Math.max(...a.dailySpending.map(d => d.amount), 1);
  }

  // ── Expense Streak / Gamification ──────────────────────────────
  private calculateStreak(expenses: ExpenseDto[]) {
    if (!expenses.length) { this.currentStreak.set(0); this.longestStreak.set(0); this.streakMessage.set('Start logging!'); return; }
    const dates = [...new Set(expenses.map(e => new Date(e.expenseDate).toDateString()))].sort((a, b) => new Date(b).getTime() - new Date(a).getTime());
    let streak = 0; let longest = 0; let current = 0;
    const today = new Date(); today.setHours(0,0,0,0);
    for (let i = 0; i < dates.length; i++) {
      const d = new Date(dates[i]); d.setHours(0,0,0,0);
      const expected = new Date(today); expected.setDate(expected.getDate() - i);
      if (d.getTime() === expected.getTime()) { current++; } else break;
    }
    // longest streak
    let tempStreak = 1;
    for (let i = 1; i < dates.length; i++) {
      const prev = new Date(dates[i-1]); const curr = new Date(dates[i]);
      const diff = (prev.getTime() - curr.getTime()) / (1000*60*60*24);
      if (Math.abs(diff - 1) < 0.5) { tempStreak++; } else { longest = Math.max(longest, tempStreak); tempStreak = 1; }
    }
    longest = Math.max(longest, tempStreak);
    this.currentStreak.set(current);
    this.longestStreak.set(longest);
    const total = expenses.reduce((s, e) => s + e.amount, 0);
    this.totalGroupSaved.set(total);
    if (current >= 7) this.streakMessage.set('🏆 Legendary Tracker!');
    else if (current >= 3) this.streakMessage.set('🔥 On Fire!');
    else if (current >= 1) this.streakMessage.set('👍 Keep Going!');
    else this.streakMessage.set('📝 Log today!');
  }

  // ══════════════════════════════════════════════════════════════
  // ── SMART AI CHAT — routes questions vs expense creation ─────
  // ══════════════════════════════════════════════════════════════
  sendChatMessage() {
    if (!this.chatInput.trim()) return;
    const msg = this.chatInput.trim();
    this.chatInput = '';

    const userMsg: ChatMessage = { id: crypto.randomUUID(), role: 'user', content: msg, timestamp: new Date() };
    const loadingMsg: ChatMessage = { id: crypto.randomUUID(), role: 'assistant', content: '', timestamp: new Date(), isLoading: true };
    this.chatMessages.update(m => [...m, userMsg, loadingMsg]);
    this.isChatLoading.set(true);

    // Detect if it's a question or an expense command
    const isQuestion = this.detectQuestion(msg);

    if (isQuestion) {
      // Smart analyze mode — answer questions about expenses
      this.expenseService.analyzeAndAnswer({ groupId: this.groupId, message: msg }).subscribe({
        next: (res) => {
          this.chatMessages.update(msgs => {
            const filtered = msgs.filter(m => !m.isLoading);
            if (res.type === 'analysis') {
              // Pure analysis answer
              return [...filtered, { id: crypto.randomUUID(), role: 'assistant' as const, content: res.answer, timestamp: new Date() }];
            } else {
              // Fell through to expense creation
              const reply: ChatMessage = {
                id: crypto.randomUUID(), role: 'assistant', timestamp: new Date(),
                content: res.parseResult?.success
                  ? `✅ Created: "${res.createdExpense?.description}" — ₹${res.createdExpense?.amount}`
                  : `❌ ${res.parseResult?.errorMessage || 'Could not understand. Try "help" to see what I can do!'}`,
                createdExpense: res.createdExpense, parsedExpense: res.parseResult?.parsedExpense
              };
              return [...filtered, reply];
            }
          });
          if (res.createdExpense) {
            this.expenses.update(es => [res.createdExpense, ...es]);
            this.loadBalances();
          }
          this.isChatLoading.set(false);
        },
        error: () => {
          this.chatMessages.update(msgs => {
            const filtered = msgs.filter(m => !m.isLoading);
            return [...filtered, { id: crypto.randomUUID(), role: 'assistant' as const, content: '❌ Failed to process. Try again!', timestamp: new Date() }];
          });
          this.isChatLoading.set(false);
        }
      });
    } else {
      // Expense creation mode
      this.expenseService.parseAndCreate({ groupId: this.groupId, message: msg }).subscribe({
        next: (res) => {
          this.chatMessages.update(msgs => {
            const filtered = msgs.filter(m => !m.isLoading);
            const reply: ChatMessage = {
              id: crypto.randomUUID(), role: 'assistant', timestamp: new Date(),
              content: res.parseResult?.success
                ? `✅ Created: "${res.createdExpense?.description}" — ₹${res.createdExpense?.amount} (${res.parseResult?.parsedExpense?.splitType} split, ${Math.round(res.parseResult?.confidence * 100)}% confidence)`
                : `❌ ${res.parseResult?.errorMessage || 'Could not parse. Try: "I paid 500 for dinner"'}`,
              createdExpense: res.createdExpense, parsedExpense: res.parseResult?.parsedExpense
            };
            return [...filtered, reply];
          });
          if (res.createdExpense) {
            this.expenses.update(es => [res.createdExpense, ...es]);
            this.loadBalances();
          }
          this.isChatLoading.set(false);
        },
        error: () => {
          this.chatMessages.update(msgs => {
            const filtered = msgs.filter(m => !m.isLoading);
            return [...filtered, { id: crypto.randomUUID(), role: 'assistant' as const, content: '❌ Failed to process. Try again!', timestamp: new Date() }];
          });
          this.isChatLoading.set(false);
        }
      });
    }
  }

  /** Detects if the message is a question/query vs an expense to log */
  private detectQuestion(msg: string): boolean {
    const lower = msg.toLowerCase();
    const questionPatterns = [
      'how much', 'do i owe', 'i owe', 'my balance', 'who owes', 'owes me',
      'why', 'reason', 'explain', 'total', 'summary', 'overview', 'status',
      'last', 'recent', 'history', 'my expense', 'my spending', 'i spent',
      'settle', 'how to', 'help', 'what can', 'commands', 'features',
      'kitna', 'kyu', 'kaun', 'kya', 'get back', 'maine',
      '?', 'tell me', 'show me', 'list'
    ];
    return questionPatterns.some(p => lower.includes(p));
  }

  toggleTheme() { this.theme.toggle(); }
  getInitials(name: string): string { return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2); }
  getCurrencySymbol(code: string): string { return code === 'INR' ? '₹' : code === 'USD' ? '$' : code === 'EUR' ? '€' : code === 'GBP' ? '£' : code; }
  getCategoryIcon(cat: string): string {
    const map: Record<string, string> = { Food: '🍕', Transport: '🚗', Entertainment: '🎬', Shopping: '🛍️', Utilities: '💡', Rent: '🏠', Travel: '✈️', Healthcare: '🏥', Education: '📚', Groceries: '🥬', General: '📋', Other: '📦' };
    return map[cat] || '📋';
  }
  getBalanceBarWidth(b: BalanceDto): number {
    const max = Math.max(...this.balances().map(x => Math.abs(x.netBalance)), 1);
    return Math.min((Math.abs(b.netBalance) / max) * 100, 100);
  }

  hasNativeShare(): boolean { return !!navigator.share; }
  encodeURI(url: string): string { return encodeURIComponent(url); }

  /** Formats bot message — converts \n to <br> and **bold** to <b> */
  formatBotMessage(content: string): string {
    if (!content) return '';
    return content
      .replace(/\*\*(.*?)\*\*/g, '<b>$1</b>')
      .replace(/\n/g, '<br>');
  }
}
