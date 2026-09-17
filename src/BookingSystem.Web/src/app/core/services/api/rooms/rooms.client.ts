import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { IRoom, IRoomRequest } from '../../../entities/rooms/room.dto';
import { IRoomSchedule } from '../../../entities/rooms/room-schedule.dto';

@Injectable({ providedIn: 'root' })
export class RoomsClient {
  private readonly http = inject(HttpClient);

  getRooms$(): Observable<IRoom[]> {
    return this.http.get<IRoom[]>('/api/rooms');
  }

  getRoom$(roomId: string): Observable<IRoom> {
    return this.http.get<IRoom>(`/api/rooms/${roomId}`);
  }

  getSchedule$(roomId: string, date?: string): Observable<IRoomSchedule> {
    return this.http.get<IRoomSchedule>(`/api/rooms/${roomId}/schedule`, {
      params: date ? { date } : {},
    });
  }

  createRoom$(request: IRoomRequest): Observable<IRoom> {
    return this.http.post<IRoom>('/api/rooms', request);
  }

  updateRoom$(roomId: string, request: IRoomRequest): Observable<IRoom> {
    return this.http.put<IRoom>(`/api/rooms/${roomId}`, request);
  }

  deactivateRoom$(roomId: string): Observable<void> {
    return this.http.delete<void>(`/api/rooms/${roomId}`);
  }
}
