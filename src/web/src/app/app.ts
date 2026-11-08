import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppShell } from './shared/layout/app-shell/app-shell';

@Component({
  imports: [RouterOutlet, AppShell],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
