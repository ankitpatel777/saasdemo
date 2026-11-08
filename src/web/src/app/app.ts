import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppHeader } from './shared/components/app-header/app-header';

@Component({
  imports: [RouterOutlet, AppHeader],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
