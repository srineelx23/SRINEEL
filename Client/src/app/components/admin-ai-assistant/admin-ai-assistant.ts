import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminChatService } from '../../services/admin-chat.service';

interface AdminMessage {
  sender: 'admin' | 'assistant';
  text: string;
  timestamp: Date;
  confidence?: string;
  rulesApplied?: string[];
}

@Component({
  selector: 'app-admin-ai-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-ai-assistant.html',
  styleUrl: './admin-ai-assistant.css'
})
export class AdminAiAssistantComponent {
  private readonly adminChatService = inject(AdminChatService);

  @ViewChild('scrollContainer') scrollContainer?: ElementRef<HTMLDivElement>;
  @ViewChild('adminInput') adminInput?: ElementRef<HTMLInputElement>;

  isOpen = signal(false);
  isSending = signal(false);
  question = signal('');

  position = signal({ x: 0, y: 84 });
  size = signal({ width: 760, height: 520 });
  isDragging = false;
  isResizing = false;
  private hasMoved = false;
  private dragOffset = { x: 0, y: 0 };
  private lastMouseCoords = { x: 0, y: 0 };

  messages = signal<AdminMessage[]>([
    {
      sender: 'assistant',
      text: 'Athena Admin Assistant online. Ask about claims, policy eligibility, user patterns, referrals, and rule enforcement.',
      timestamp: new Date(),
      confidence: 'HIGH'
    }
  ]);

  ngOnInit(): void {
    this.loadPosition();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.isOpen.set(false);
  }

  @HostListener('document:keydown', ['$event'])
  onShortcut(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      this.togglePanel();
    }
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent): void {
    if (this.isDragging) {
      this.position.set({
        x: event.clientX - this.dragOffset.x,
        y: Math.max(16, event.clientY - this.dragOffset.y)
      });
      return;
    }

    if (this.isResizing) {
      const dx = event.clientX - this.lastMouseCoords.x;
      const dy = event.clientY - this.lastMouseCoords.y;

      this.size.update(s => ({
        width: Math.max(520, s.width + dx),
        height: Math.max(360, s.height + dy)
      }));

      this.lastMouseCoords = { x: event.clientX, y: event.clientY };
    }
  }

  @HostListener('document:mouseup')
  onMouseUp(): void {
    if (this.isDragging || this.isResizing) {
      this.savePosition();
    }

    this.isDragging = false;
    this.isResizing = false;
  }

  togglePanel(): void {
    this.isOpen.update(v => !v);
    if (this.isOpen()) {
      if (!this.hasMoved) {
        this.centerPanel();
      }

      setTimeout(() => this.adminInput?.nativeElement?.focus(), 50);
      this.scrollToBottom();
    }
  }

  startDrag(event: MouseEvent): void {
    if ((event.target as HTMLElement).closest('.close-btn') || (event.target as HTMLElement).tagName === 'INPUT') {
      return;
    }

    this.isDragging = true;
    this.hasMoved = true;
    this.dragOffset = {
      x: event.clientX - this.position().x,
      y: event.clientY - this.position().y
    };
    event.preventDefault();
  }

  startResize(event: MouseEvent): void {
    this.isResizing = true;
    this.hasMoved = true;
    this.lastMouseCoords = { x: event.clientX, y: event.clientY };
    event.preventDefault();
    event.stopPropagation();
  }

  sendQuestion(): void {
    const text = this.question().trim();
    if (!text || this.isSending()) {
      return;
    }

    this.messages.update(prev => [
      ...prev,
      {
        sender: 'admin',
        text,
        timestamp: new Date()
      }
    ]);

    this.question.set('');
    this.isSending.set(true);
    this.scrollToBottom();

    const history = this.messages()
      .slice(-8)
      .map(m => `${m.sender === 'admin' ? 'ADMIN' : 'ATHENA'}: ${m.text}`);

    this.adminChatService.ask(text, history).subscribe({
      next: (res) => {
        this.messages.update(prev => [
          ...prev,
          {
            sender: 'assistant',
            text: this.sanitizeAssistantAnswer(res?.answer) || 'Insufficient data to answer.',
            timestamp: new Date(),
            confidence: res?.confidence || 'LOW',
            rulesApplied: Array.isArray(res?.rulesApplied) ? res.rulesApplied : []
          }
        ]);
        this.isSending.set(false);
        this.scrollToBottom();
      },
      error: () => {
        this.messages.update(prev => [
          ...prev,
          {
            sender: 'assistant',
            text: 'Assistant is temporarily unavailable. Please try again.',
            timestamp: new Date(),
            confidence: 'LOW'
          }
        ]);
        this.isSending.set(false);
        this.scrollToBottom();
      }
    });
  }

  applyPrompt(prompt: string): void {
    if (this.isSending()) {
      return;
    }

    this.question.set(prompt);
    this.sendQuestion();
  }

  formatTime(value: Date): string {
    return new Intl.DateTimeFormat('en-US', {
      hour: '2-digit',
      minute: '2-digit'
    }).format(value);
  }

  trackByIndex(index: number): number {
    return index;
  }

  private sanitizeAssistantAnswer(answer?: string): string {
    const text = (answer || '').trim();
    if (!text) {
      return '';
    }

    // Remove appended rules block formats if the model includes them in plain text.
    return text
      .replace(/\n?\s*rules\s*applied\s*:\s*[\s\S]*$/i, '')
      .replace(/\n?\s*rules\s*:\s*[\s\S]*$/i, '')
      .trim();
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const container = this.scrollContainer?.nativeElement;
      if (!container) {
        return;
      }
      container.scrollTop = container.scrollHeight;
    });
  }

  private centerPanel(): void {
    if (typeof window === 'undefined') {
      return;
    }

    this.position.set({
      x: Math.max(16, (window.innerWidth - this.size().width) / 2),
      y: 84
    });
  }

  private loadPosition(): void {
    const raw = localStorage.getItem('vims_admin_ai_panel');
    if (!raw) {
      return;
    }

    try {
      const parsed = JSON.parse(raw);
      if (parsed?.position && parsed?.size) {
        this.position.set(parsed.position);
        this.size.set(parsed.size);
        this.hasMoved = true;
      }
    } catch {
      // ignore invalid local storage payload
    }
  }

  private savePosition(): void {
    localStorage.setItem('vims_admin_ai_panel', JSON.stringify({
      position: this.position(),
      size: this.size()
    }));
  }
}
