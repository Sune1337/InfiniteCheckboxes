import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Highscore } from './models/highscore';

@Injectable({
  providedIn: 'root'
})
export class HighscoreApiService {

  private httpClient = inject(HttpClient);

  public getHighscores = (name: string): Observable<Highscore> => {
    return this.httpClient.get<Highscore>(`/api/v1/HighscoreAPI/GetHighscores?name=${name}`);
  }
}
