import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ExpenseDto, CreateExpenseRequest, BalanceDto, DebtSimplificationDto, PaginatedResponse,
  SettlementDto, CreateSettlementRequest, UpiDeepLinkResponse, AiParseRequest, AiParseResponse,
  SpendingAnalyticsDto, SettlementBreakdownResponse
} from '../models/interfaces';

@Injectable({ providedIn: 'root' })
export class ExpenseService {
  private readonly API = `${environment.apiUrl}/expenses`;
  private readonly SETTLE_API = `${environment.apiUrl}/settlements`;
  private readonly AI_API = `${environment.apiUrl}/aiassistant`;

  constructor(private http: HttpClient) {}

  // ── Expenses ──────────────────────────────────────────────────────
  getGroupExpenses(groupId: string, page = 1, pageSize = 20): Observable<PaginatedResponse<ExpenseDto>> {
    return this.http.get<PaginatedResponse<ExpenseDto>>(`${this.API}/group/${groupId}`, { params: { page, pageSize } });
  }

  getExpense(id: string): Observable<ExpenseDto> {
    return this.http.get<ExpenseDto>(`${this.API}/${id}`);
  }

  createExpense(req: CreateExpenseRequest): Observable<ExpenseDto> {
    return this.http.post<ExpenseDto>(this.API, req);
  }

  deleteExpense(id: string): Observable<any> {
    return this.http.delete(`${this.API}/${id}`);
  }

  getGroupBalances(groupId: string): Observable<BalanceDto[]> {
    return this.http.get<BalanceDto[]>(`${this.API}/group/${groupId}/balances`);
  }

  getSimplifiedDebts(groupId: string): Observable<DebtSimplificationDto[]> {
    return this.http.get<DebtSimplificationDto[]>(`${this.API}/group/${groupId}/simplify`);
  }

  // ── Spending Analytics ────────────────────────────────────────────
  getSpendingAnalytics(groupId: string): Observable<SpendingAnalyticsDto> {
    return this.http.get<SpendingAnalyticsDto>(`${this.API}/group/${groupId}/analytics`);
  }

  // ── Settlements ───────────────────────────────────────────────────
  getGroupSettlements(groupId: string): Observable<SettlementDto[]> {
    return this.http.get<SettlementDto[]>(`${this.SETTLE_API}/group/${groupId}`);
  }

  createSettlement(req: CreateSettlementRequest): Observable<SettlementDto> {
    return this.http.post<SettlementDto>(this.SETTLE_API, req);
  }

  confirmSettlement(id: string): Observable<SettlementDto> {
    return this.http.put<SettlementDto>(`${this.SETTLE_API}/${id}/confirm`, {});
  }

  getUpiLink(receiverUserId: string, amount: number, groupId: string): Observable<UpiDeepLinkResponse> {
    return this.http.get<UpiDeepLinkResponse>(`${this.SETTLE_API}/upi-link`, { params: { receiverUserId, amount, groupId } });
  }

  getSettlementBreakdown(groupId: string, receiverUserId: string): Observable<SettlementBreakdownResponse> {
    return this.http.get<SettlementBreakdownResponse>(`${this.SETTLE_API}/breakdown/${groupId}/${receiverUserId}`);
  }

  // ── AI Assistant ──────────────────────────────────────────────────
  parseMessage(req: AiParseRequest): Observable<AiParseResponse> {
    return this.http.post<AiParseResponse>(`${this.AI_API}/parse`, req);
  }

  parseAndCreate(req: AiParseRequest): Observable<any> {
    return this.http.post<any>(`${this.AI_API}/create`, req);
  }

  /** Smart AI — answers questions about expenses/balances */
  analyzeAndAnswer(req: AiParseRequest): Observable<any> {
    return this.http.post<any>(`${this.AI_API}/analyze`, req);
  }
}
