import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface TripMemberDto {
  userId: string;
  displayName: string;
  avatarUrl?: string;
  joinedAt: string;
  isAdmin: boolean;
}

export interface TripDto {
  id: string;
  name: string;
  description?: string;
  destination?: string;
  budget: number;
  totalSpent: number;
  remainingBudget: number;
  currency: string;
  startDate: string;
  endDate?: string;
  tripCode: string;
  qrCodeUrl: string;
  adminUserId: string;
  adminDisplayName: string;
  isActive: boolean;
  isAdmin: boolean;
  createdAt: string;
  members: TripMemberDto[];
}

export interface TripExpenseDto {
  id: string;
  tripId: string;
  addedByUserId: string;
  addedByDisplayName: string;
  description: string;
  reason: string;
  amount: number;
  currency: string;
  spentAt: string;
  screenshotData?: string;
  category: string;
  isRejected: boolean;
  rejectionReason?: string;
  rejectedByDisplayName?: string;
  rejectedAt?: string;
  createdAt: string;
}

export interface CreateTripRequest {
  name: string;
  description?: string;
  destination?: string;
  budget: number;
  currency: string;
  startDate: string;
  endDate?: string;
}

export interface AddTripExpenseRequest {
  description: string;
  reason: string;
  amount: number;
  currency: string;
  spentAt: string;
  screenshotData?: string;
  category: string;
}

@Injectable({ providedIn: 'root' })
export class TripService {
  private readonly API = `${environment.apiUrl}/trips`;

  constructor(private http: HttpClient) {}

  getMyTrips(): Observable<TripDto[]> {
    return this.http.get<TripDto[]>(this.API);
  }

  getTrip(id: string): Observable<TripDto> {
    return this.http.get<TripDto>(`${this.API}/${id}`);
  }

  createTrip(req: CreateTripRequest): Observable<TripDto> {
    return this.http.post<TripDto>(this.API, req);
  }

  joinTrip(tripCode: string): Observable<TripDto> {
    return this.http.post<TripDto>(`${this.API}/join`, { tripCode });
  }

  getJoinInfo(code: string): Observable<any> {
    return this.http.get<any>(`${this.API}/join-info/${code}`);
  }

  updateBudget(id: string, budget: number): Observable<TripDto> {
    return this.http.put<TripDto>(`${this.API}/${id}/budget`, { budget });
  }

  getExpenses(tripId: string): Observable<TripExpenseDto[]> {
    return this.http.get<TripExpenseDto[]>(`${this.API}/${tripId}/expenses`);
  }

  addExpense(tripId: string, req: AddTripExpenseRequest): Observable<TripExpenseDto> {
    return this.http.post<TripExpenseDto>(`${this.API}/${tripId}/expenses`, req);
  }

  rejectExpense(tripId: string, expId: string, reason: string): Observable<TripExpenseDto> {
    return this.http.put<TripExpenseDto>(`${this.API}/${tripId}/expenses/${expId}/reject`, { reason });
  }

  restoreExpense(tripId: string, expId: string): Observable<TripExpenseDto> {
    return this.http.put<TripExpenseDto>(`${this.API}/${tripId}/expenses/${expId}/restore`, {});
  }

  deleteExpense(tripId: string, expId: string, reason: string): Observable<any> {
    return this.http.delete(`${this.API}/${tripId}/expenses/${expId}`, { body: { reason } });
  }

  tripChat(tripId: string, message: string): Observable<{ answer: string; timestamp: string }> {
    return this.http.post<{ answer: string; timestamp: string }>(`${this.API}/${tripId}/chat`, { message });
  }
}
