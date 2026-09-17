import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IAdminBooking } from '../../../entities/bookings/admin-booking.dto';
import { IAdminBookingsCriteria } from '../../../entities/bookings/admin-bookings.criteria';
import { IBooking, IBookSlotRequest } from '../../../entities/bookings/booking.dto';
import { IMyBooking } from '../../../entities/bookings/my-booking.dto';

@Injectable({ providedIn: 'root' })
export class BookingsClient {
  private readonly http = inject(HttpClient);

  getMyBookings$(): Observable<IMyBooking[]> {
    return this.http.get<IMyBooking[]>('/api/bookings');
  }

  getAllBookings$(criteria: IAdminBookingsCriteria): Observable<IAdminBooking[]> {
    const params = Object.entries(criteria)
      .filter(([, value]) => value !== null && value !== undefined && value !== '')
      .reduce((all, [key, value]) => all.set(key, String(value)), new HttpParams());

    return this.http.get<IAdminBooking[]>('/api/admin/bookings', { params });
  }

  bookSlot$(request: IBookSlotRequest): Observable<IBooking> {
    return this.http.post<IBooking>('/api/bookings', request);
  }

  cancelBooking$(bookingId: string): Observable<void> {
    return this.http.delete<void>(`/api/bookings/${bookingId}`);
  }
}
