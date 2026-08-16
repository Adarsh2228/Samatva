import { Component, signal, OnInit, inject, computed } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthService } from '../../services/auth.service';
import { TripService, TripDto, TripExpenseDto, AddTripExpenseRequest } from '../../services/trip.service';
import { ThemeService } from '../../services/theme.service';

type TripView = 'list' | 'detail';

@Component({
  selector: 'app-trips',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DatePipe, DecimalPipe],
  templateUrl: './trips.component.html',
  styleUrl: './trips.component.scss'
})
export class TripsComponent implements OnInit {
  private auth = inject(AuthService);
  private tripService = inject(TripService);
  private theme = inject(ThemeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private sanitizer = inject(DomSanitizer);

  isDark = this.theme.isDark;
  user = this.auth.user;

  // ── View State ─────────────────────────────────────────────────
  view = signal<TripView>('list');
  isLoading = signal(true);

  // ── Trips List ─────────────────────────────────────────────────
  trips = signal<TripDto[]>([]);

  // ── Active Trip Detail ─────────────────────────────────────────
  activeTrip = signal<TripDto | null>(null);
  expenses = signal<TripExpenseDto[]>([]);
  expensesLoading = signal(false);

  // ── Computed ───────────────────────────────────────────────────
  activeExpenses = computed(() => this.expenses().filter(e => !e.isRejected));
  rejectedExpenses = computed(() => this.expenses().filter(e => e.isRejected));
  budgetPercent = computed(() => {
    const t = this.activeTrip();
    if (!t || t.budget <= 0) return 0;
    return Math.min((t.totalSpent / t.budget) * 100, 100);
  });

  // ── Create Trip Modal ──────────────────────────────────────────
  showCreateModal = signal(false);
  createForm = { name: '', description: '', destination: '', budget: 0, currency: 'INR', startDate: '', endDate: '' };
  createLoading = signal(false);
  createError = signal('');

  // ── Join Trip Modal ────────────────────────────────────────────
  showJoinModal = signal(false);
  joinCode = '';
  joinInfo = signal<any>(null);
  joinLoading = signal(false);
  joinError = signal('');

  // ── Add Expense Modal ──────────────────────────────────────────
  showAddExpense = signal(false);
  expenseForm: AddTripExpenseRequest = {
    description: '', reason: '', amount: 0, currency: 'INR',
    spentAt: new Date().toISOString().slice(0, 16), screenshotData: '', category: 'Food'
  };
  expenseLoading = signal(false);
  expenseError = signal('');
  imageUploading = signal(false);
  imagePreview = signal<string>('');

  // ── Expense Detail Card ────────────────────────────────────────
  selectedExpense = signal<TripExpenseDto | null>(null);

  // ── Admin: Reject Modal ────────────────────────────────────────
  showRejectModal = signal(false);
  rejectTargetExpense = signal<TripExpenseDto | null>(null);
  rejectReason = '';
  rejectLoading = signal(false);

  // ── Update Budget Modal ───────────────────────────────────────
  showBudgetModal = signal(false);
  newBudget = 0;
  budgetLoading = signal(false);

  // ── AI Chat Panel ──────────────────────────────────────────────
  showChat = signal(false);
  chatMessages = signal<{ role: 'user' | 'ai'; text: string; time: Date }[]>([]);
  chatInput = '';
  chatLoading = signal(false);
  chatSuggestions = ['Show trip summary', 'Who paid the most?', 'Budget status', 'Category breakdown', 'Show recent expenses'];

  // ── Expense View Filter ────────────────────────────────────────
  expenseFilter = signal<'all' | 'mine'>('all');

  // ── Share / QR Modal ──────────────────────────────────────────
  showShareModal = signal(false);
  codeCopied = signal(false);

  readonly CATEGORIES = ['Food', 'Transport', 'Hotel', 'Entertainment', 'Shopping', 'Sightseeing', 'Medical', 'Other'];
  readonly Math = Math;

  ngOnInit() {
    if (!this.auth.isAuthenticated()) { this.router.navigate(['/login']); return; }
    this.loadTrips();
    // Check if opening a trip directly via URL query param
    this.route.queryParams.subscribe(params => {
      if (params['code']) {
        this.joinCode = params['code'];
        this.showJoinModal.set(true);
        this.lookupJoinCode();
      }
    });
  }

  // ── Load All Trips ─────────────────────────────────────────────
  loadTrips() {
    this.isLoading.set(true);
    this.tripService.getMyTrips().subscribe({
      next: (t) => { this.trips.set(t); this.isLoading.set(false); },
      error: () => this.isLoading.set(false)
    });
  }

  // ── Open Trip Detail ────────────────────────────────────────────
  openTrip(trip: TripDto) {
    this.activeTrip.set(trip);
    this.view.set('detail');
    this.loadExpenses(trip.id);
  }

  loadExpenses(tripId: string) {
    this.expensesLoading.set(true);
    this.tripService.getExpenses(tripId).subscribe({
      next: (e) => { this.expenses.set(e); this.expensesLoading.set(false); },
      error: () => this.expensesLoading.set(false)
    });
  }

  refreshTrip() {
    const t = this.activeTrip();
    if (!t) return;
    this.tripService.getTrip(t.id).subscribe(updated => {
      this.activeTrip.set(updated);
      // update in list too
      this.trips.update(list => list.map(x => x.id === updated.id ? updated : x));
    });
  }

  goBack() {
    this.view.set('list');
    this.activeTrip.set(null);
    this.expenses.set([]);
  }

  // ── Create Trip ────────────────────────────────────────────────
  openCreateModal() {
    const today = new Date().toISOString().split('T')[0];
    this.createForm = { name: '', description: '', destination: '', budget: 0, currency: 'INR', startDate: today, endDate: '' };
    this.createError.set('');
    this.showCreateModal.set(true);
  }

  submitCreateTrip() {
    if (!this.createForm.name.trim()) { this.createError.set('Trip name is required.'); return; }
    if (this.createForm.budget <= 0) { this.createError.set('Budget must be greater than 0.'); return; }
    this.createLoading.set(true);
    this.tripService.createTrip({
      name: this.createForm.name.trim(),
      description: this.createForm.description || undefined,
      destination: this.createForm.destination || undefined,
      budget: this.createForm.budget,
      currency: this.createForm.currency,
      startDate: this.createForm.startDate ? new Date(this.createForm.startDate).toISOString() : new Date().toISOString(),
      endDate: this.createForm.endDate ? new Date(this.createForm.endDate).toISOString() : undefined
    }).subscribe({
      next: (t) => {
        this.createLoading.set(false);
        this.showCreateModal.set(false);
        this.trips.update(list => [t, ...list]);
        this.openTrip(t);
      },
      error: (e) => { this.createLoading.set(false); this.createError.set(e?.error?.message || 'Failed to create trip.'); }
    });
  }

  // ── Join Trip ──────────────────────────────────────────────────
  lookupJoinCode() {
    if (this.joinCode.trim().length < 4) return;
    this.joinLoading.set(true);
    this.joinInfo.set(null);
    this.joinError.set('');
    this.tripService.getJoinInfo(this.joinCode.trim().toUpperCase()).subscribe({
      next: (info) => { this.joinInfo.set(info); this.joinLoading.set(false); },
      error: () => { this.joinLoading.set(false); this.joinError.set('Invalid trip code. Please check and try again.'); }
    });
  }

  submitJoinTrip() {
    this.joinLoading.set(true);
    this.tripService.joinTrip(this.joinCode.trim().toUpperCase()).subscribe({
      next: (t) => {
        this.joinLoading.set(false);
        this.showJoinModal.set(false);
        this.joinCode = ''; this.joinInfo.set(null);
        this.trips.update(list => {
          const exists = list.some(x => x.id === t.id);
          return exists ? list.map(x => x.id === t.id ? t : x) : [t, ...list];
        });
        this.openTrip(t);
      },
      error: (e) => { this.joinLoading.set(false); this.joinError.set(e?.error?.message || 'Failed to join trip.'); }
    });
  }

  // ── Add Expense ────────────────────────────────────────────────
  openAddExpense() {
    this.expenseForm = {
      description: '', reason: '', amount: 0, currency: this.activeTrip()?.currency || 'INR',
      spentAt: new Date().toISOString().slice(0, 16), screenshotData: '', category: 'Food'
    };
    this.expenseError.set('');
    this.imagePreview.set('');
    this.showAddExpense.set(true);
  }

  submitExpense() {
    if (!this.expenseForm.description.trim()) { this.expenseError.set('Description is required.'); return; }
    if (!this.expenseForm.reason.trim()) { this.expenseError.set('Reason is required.'); return; }
    if (this.expenseForm.amount <= 0) { this.expenseError.set('Amount must be greater than 0.'); return; }
    const trip = this.activeTrip();
    if (!trip) return;
    this.expenseLoading.set(true);
    this.tripService.addExpense(trip.id, {
      ...this.expenseForm,
      spentAt: new Date(this.expenseForm.spentAt).toISOString(),
      screenshotData: this.expenseForm.screenshotData || undefined
    }).subscribe({
      next: (e) => {
        this.expenseLoading.set(false);
        this.showAddExpense.set(false);
        this.expenses.update(list => [e, ...list]);
        this.refreshTrip();
      },
      error: (e) => { this.expenseLoading.set(false); this.expenseError.set(e?.error?.message || 'Failed to add expense.'); }
    });
  }

  // ── Expense Detail ─────────────────────────────────────────────
  openExpenseDetail(e: TripExpenseDto) { this.selectedExpense.set(e); }
  closeExpenseDetail() { this.selectedExpense.set(null); }

  // ── Admin: Reject ──────────────────────────────────────────────
  openRejectModal(e: TripExpenseDto) {
    this.rejectTargetExpense.set(e);
    this.rejectReason = '';
    this.showRejectModal.set(true);
    this.selectedExpense.set(null);
  }

  submitReject() {
    const exp = this.rejectTargetExpense();
    const trip = this.activeTrip();
    if (!exp || !trip || !this.rejectReason.trim()) return;
    this.rejectLoading.set(true);
    this.tripService.rejectExpense(trip.id, exp.id, this.rejectReason.trim()).subscribe({
      next: (updated) => {
        this.rejectLoading.set(false);
        this.showRejectModal.set(false);
        this.expenses.update(list => list.map(x => x.id === updated.id ? updated : x));
        this.refreshTrip();
      },
      error: () => this.rejectLoading.set(false)
    });
  }

  restoreExpense(e: TripExpenseDto) {
    const trip = this.activeTrip();
    if (!trip) return;
    this.tripService.restoreExpense(trip.id, e.id).subscribe({
      next: (updated) => {
        this.expenses.update(list => list.map(x => x.id === updated.id ? updated : x));
        this.refreshTrip();
      }
    });
  }

  // ── Update Budget ──────────────────────────────────────────────
  openBudgetModal() {
    this.newBudget = this.activeTrip()?.budget || 0;
    this.showBudgetModal.set(true);
  }

  submitBudget() {
    const trip = this.activeTrip();
    if (!trip || this.newBudget <= 0) return;
    this.budgetLoading.set(true);
    this.tripService.updateBudget(trip.id, this.newBudget).subscribe({
      next: (t) => {
        this.budgetLoading.set(false);
        this.showBudgetModal.set(false);
        this.activeTrip.set(t);
        this.trips.update(list => list.map(x => x.id === t.id ? t : x));
      },
      error: () => this.budgetLoading.set(false)
    });
  }

  // ── Share / QR ─────────────────────────────────────────────────
  copyCode() {
    const t = this.activeTrip();
    if (!t) return;
    navigator.clipboard.writeText(t.tripCode).then(() => {
      this.codeCopied.set(true);
      setTimeout(() => this.codeCopied.set(false), 2000);
    });
  }

  // ── AI Chat ────────────────────────────────────────────────────
  sendChatMessage(msg?: string) {
    const text = (msg ?? this.chatInput).trim();
    if (!text || this.chatLoading()) return;
    const trip = this.activeTrip();
    if (!trip) return;
    this.chatMessages.update(m => [...m, { role: 'user', text, time: new Date() }]);
    this.chatInput = '';
    this.chatLoading.set(true);
    this.tripService.tripChat(trip.id, text).subscribe({
      next: (r) => {
        this.chatLoading.set(false);
        this.chatMessages.update(m => [...m, { role: 'ai', text: r.answer, time: new Date() }]);
        setTimeout(() => {
          const el = document.getElementById('chat-bottom');
          el?.scrollIntoView({ behavior: 'smooth' });
        }, 100);
      },
      error: () => {
        this.chatLoading.set(false);
        this.chatMessages.update(m => [...m, { role: 'ai', text: '⚠️ Could not get a response. Please try again.', time: new Date() }]);
      }
    });
  }

  useSuggestion(s: string) { this.sendChatMessage(s); }

  openChat() {
    this.showChat.set(true);
    if (this.chatMessages().length === 0) {
      const t = this.activeTrip();
      this.chatMessages.set([{
        role: 'ai',
        text: `👋 Hi! I'm your **Trip AI Assistant** for **${t?.name}**.\n\nI can help you with budget, spending analysis, category breakdowns, and more!\n\nTry asking: *"Show summary"*, *"Who paid most?"*, or *"Budget status"*`,
        time: new Date()
      }]);
    }
  }

  formatMarkdown(text: string): SafeHtml {
    const html = text
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/\n/g, '<br>');
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  filteredActiveExpenses() {
    const uid = this.user()?.id;
    if (this.expenseFilter() === 'mine' && uid) {
      return this.activeExpenses().filter(e => e.addedByUserId === uid);
    }
    return this.activeExpenses();
  }

  // ── Image Upload ────────────────────────────────────────────
  onImageSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) { this.expenseError.set('Image must be under 2MB'); return; }
    this.imageUploading.set(true);
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = reader.result as string;
      this.expenseForm.screenshotData = base64;
      this.imagePreview.set(base64);
      this.imageUploading.set(false);
    };
    reader.readAsDataURL(file);
  }

  clearImage(): void {
    this.expenseForm.screenshotData = '';
    this.imagePreview.set('');
  }

  // ── Helpers ─────────────────────────────────────────────────────
  hideImg(event: Event) {
    (event.target as HTMLImageElement).style.display = 'none';
  }

  getInitials(name: string): string {
    return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
  }

  getCurrencySymbol(c: string): string {
    return c === 'INR' ? '₹' : c === 'USD' ? '$' : c === 'EUR' ? '€' : c;
  }

  getCategoryIcon(cat: string): string {
    const m: Record<string, string> = {
      Food: '🍽️', Transport: '🚗', Hotel: '🏨', Entertainment: '🎉',
      Shopping: '🛍️', Sightseeing: '🗺️', Medical: '💊', Other: '📦', General: '📦'
    };
    return m[cat] || '📦';
  }

  getCategoryColor(cat: string): string {
    const m: Record<string, string> = {
      Food: '#FF6B6B', Transport: '#74B9FF', Hotel: '#FDCB6E',
      Entertainment: '#A29BFE', Shopping: '#FD79A8', Sightseeing: '#00CEC9',
      Medical: '#55EFC4', Other: '#636E72', General: '#636E72'
    };
    return m[cat] || '#636E72';
  }

  getBudgetClass(): string {
    const p = this.budgetPercent();
    if (p >= 100) return 'over-budget';
    if (p >= 80) return 'near-budget';
    return 'on-budget';
  }

  formatCode(code: string): string {
    return code ? code.slice(0, 4) + ' ' + code.slice(4) : '';
  }
}
