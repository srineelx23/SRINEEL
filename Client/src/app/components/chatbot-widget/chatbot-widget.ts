import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, ViewChild, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatbotHistoryItem, ChatbotService } from '../../services/chatbot.service';

interface UiMessage {
    sender: 'user' | 'bot';
    text: string;
    timestamp: Date;
}

@Component({
    selector: 'app-chatbot-widget',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './chatbot-widget.html',
    styleUrl: './chatbot-widget.css'
})
export class ChatbotWidgetComponent implements OnInit {
    private chatbotService = inject(ChatbotService);
    @ViewChild('messagesContainer') messagesContainer?: ElementRef<HTMLDivElement>;
    @ViewChild('chatbotInput') inputElement?: ElementRef<HTMLInputElement>;

    isOpen = signal(false);
    isSending = signal(false);
    inputText = signal('');
    messages = signal<UiMessage[]>([
        {
            sender: 'bot',
            text: 'I am your Vehicle Insurance Assistant. How can I help you today?',
            timestamp: new Date()
        }
    ]);

    // Position & Size state
    position = signal({ x: 0, y: 100 }); // initial y: 100px from top
    size = signal({ width: 700, height: 500 }); // initial size
    isDragging = false;
    isResizing = false;
    dragOffset = { x: 0, y: 0 };
    lastMouseCoords = { x: 0, y: 0 };
    private hasMoved = false;

    ngOnInit() {
        this.loadPosition();
    }

    private loadPosition() {
        const saved = localStorage.getItem('vims_chatbot_pos');
        if (saved) {
            try {
                const parsed = JSON.parse(saved);
                this.position.set(parsed.pos);
                this.size.set(parsed.size);
                this.hasMoved = true;
            } catch {}
        }
    }

    private savePosition() {
        localStorage.setItem('vims_chatbot_pos', JSON.stringify({
            pos: this.position(),
            size: this.size()
        }));
    }

    @HostListener('document:keydown.escape')
    onEscape(): void {
        this.isOpen.set(false);
    }

    // Centering helper
    private centerWidget() {
        if (typeof window !== 'undefined') {
            const x = (window.innerWidth - this.size().width) / 2;
            const y = 100;
            this.position.set({ x, y });
        }
    }

    // A nice spotlight shortcut: Ctrl + K or Cmd + K
    @HostListener('document:keydown', ['$event'])
    onKeydownHandler(event: KeyboardEvent) {
        if ((event.metaKey || event.ctrlKey) && event.key === 'k') {
            event.preventDefault();
            this.toggle();
        }
    }

    // Drag Logic
    startDrag(event: MouseEvent) {
        if ((event.target as HTMLElement).closest('.close-button') || (event.target as HTMLElement).tagName === 'INPUT') return;
        this.isDragging = true;
        this.hasMoved = true;
        this.dragOffset = {
            x: event.clientX - this.position().x,
            y: event.clientY - this.position().y
        };
        event.preventDefault();
    }

    // Resize Logic
    startResize(event: MouseEvent) {
        this.isResizing = true;
        this.hasMoved = true;
        this.lastMouseCoords = { x: event.clientX, y: event.clientY };
        event.preventDefault();
        event.stopPropagation();
    }

    @HostListener('document:mousemove', ['$event'])
    onMouseMove(event: MouseEvent) {
        if (this.isDragging) {
            this.position.set({
                x: event.clientX - this.dragOffset.x,
                y: event.clientY - this.dragOffset.y
            });
        } else if (this.isResizing) {
            const dx = event.clientX - this.lastMouseCoords.x;
            const dy = event.clientY - this.lastMouseCoords.y;
            this.size.update(s => ({
                width: Math.max(400, s.width + dx),
                height: Math.max(300, s.height + dy)
            }));
            this.lastMouseCoords = { x: event.clientX, y: event.clientY };
        }
    }

    @HostListener('document:mouseup')
    onMouseUp() {
        if (this.isDragging || this.isResizing) {
            this.savePosition();
        }
        this.isDragging = false;
        this.isResizing = false;
    }

    toggle(): void {
        this.isOpen.update(v => !v);
        if (this.isOpen()) {
            if (!this.hasMoved) {
                this.centerWidget();
            }
            setTimeout(() => {
                this.inputElement?.nativeElement?.focus();
                this.scrollToLatest();
            }, 100);
        }
    }

    send(): void {
        const query = this.inputText().trim();
        if (!query || this.isSending()) {
            return;
        }

        const historyForRequest = this.buildHistoryForRequest(query);

        this.messages.update(prev => [
            ...prev,
            {
                sender: 'user',
                text: query,
                timestamp: new Date()
            }
        ]);
        
        this.scrollToLatest();
        this.inputText.set('');
        this.isSending.set(true);

        this.chatbotService.ask(query, historyForRequest).subscribe({
            next: (res) => {
                this.messages.update(prev => [
                    ...prev,
                    {
                        sender: 'bot',
                        text: (res.response || "I don't have that information").trim(),
                        timestamp: new Date()
                    }
                ]);
                this.isSending.set(false);
                this.scrollToLatest();
                setTimeout(() => this.inputElement?.nativeElement?.focus(), 50);
            },
            error: () => {
                this.messages.update(prev => [
                    ...prev,
                    {
                        sender: 'bot',
                        text: 'Unable to reach the assistant right now. Please try again.',
                        timestamp: new Date()
                    }
                ]);
                this.isSending.set(false);
                this.scrollToLatest();
                setTimeout(() => this.inputElement?.nativeElement?.focus(), 50);
            }
        });
    }

    trackByIndex(index: number): number {
        return index;
    }

    private scrollToLatest(): void {
        setTimeout(() => {
            const container = this.messagesContainer?.nativeElement;
            if (!container) return;
            container.scrollTop = container.scrollHeight;
        });
    }

    private buildHistoryForRequest(currentQuery: string): ChatbotHistoryItem[] {
        const intro = 'I am your Vehicle Insurance Assistant. How can I help you today?';
        const history = this.messages()
            .filter(m => {
                const text = m.text?.trim();
                return !!text && text !== intro;
            })
            .slice(-10)
            .map<ChatbotHistoryItem>(m => ({
                role: m.sender === 'bot' ? 'assistant' : 'user',
                content: m.text.trim()
            }));

        history.push({ role: 'user', content: currentQuery });
        return history;
    }
}
