// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

import * as signalR from '@microsoft/signalr';
import moment from 'moment';
import 'moment/locale/en-gb';
import { Telemetry } from './Telemetry';

export class App {
    private readonly connection: signalR.HubConnection;
    private readonly hidden = 'd-none';
    private readonly loaderSelector = '.spinner-border';

    private logsAutoscroll: HTMLInputElement;
    private logsContainer: HTMLInputElement;
    private webhooksCountContainer: HTMLElement;
    private webhooksIndexContainer: HTMLElement;
    private webhooksContentContainer: HTMLElement;

    private appId: HTMLInputElement;
    private webhookDelivery: number;
    private webhookEvent: HTMLInputElement;
    private webhookPayload: HTMLInputElement;
    private webhookSignature: HTMLInputElement;
    private webhookSubmit: HTMLElement;

    constructor() {
        this.connection = new signalR.HubConnectionBuilder().withUrl('/admin/git-hub').withAutomaticReconnect().build();
    }

    async initialize(): Promise<void> {
        Telemetry.initialize();

        const logsContainer = <HTMLInputElement>document.getElementById('logs');
        const webhookSubmit = document.getElementById('post-webhook');

        if (logsContainer) {
            await this.initializeLogs(logsContainer);
        } else if (webhookSubmit) {
            this.initiailizeDebug(webhookSubmit);
        }

        if ('ClipboardJS' in window) {
            const selector = '.copy-button';
            const copyButton = document.querySelector(selector);

            const clipboard: any = window['ClipboardJS' as any];
            new clipboard(selector);
            copyButton.addEventListener('click', (event) => {
                event.preventDefault();
            });
        }
    }

    private async initializeLogs(logsContainer: HTMLInputElement): Promise<void> {
        this.logsContainer = logsContainer;
        this.webhooksCountContainer = document.getElementById('webhooks-count');
        this.webhooksIndexContainer = document.getElementById('webhooks-index');
        this.logsAutoscroll = <HTMLInputElement>document.getElementById('logs-auto-scroll');
        this.webhooksContentContainer = document.getElementById('webhooks-content');

        moment.locale('en-gb');

        this.connection.on('application-logs', (logEntry: LogEntry) => {
            this.onApplicationLog(logEntry);
        });

        this.connection.on('webhook-logs', (webhookHeaders: any, webhookEvent: any) => {
            this.onWebhook(webhookHeaders, webhookEvent);
        });

        await this.connection.start().catch((reason: any) => console.error(reason));
    }

    private initiailizeDebug(webhookSubmit: HTMLElement) {
        this.appId = <HTMLInputElement>document.getElementById('app-id');
        this.webhookDelivery = 1;
        this.webhookEvent = <HTMLInputElement>document.getElementById('webhook-event');
        this.webhookPayload = <HTMLInputElement>document.getElementById('webhook-payload');
        this.webhookSignature = <HTMLInputElement>document.getElementById('webhook-signature');
        this.webhookSubmit = webhookSubmit;

        const onChange = () => {
            try {
                JSON.parse(this.webhookPayload.value);
                this.enable(this.webhookSubmit);
            } catch (err: any) {
                this.disable(this.webhookSubmit);
            }
        };

        ['blur', 'change', 'input', 'keydown'].forEach((name) => {
            this.webhookPayload.addEventListener(name, onChange);
        });

        const onSubmit = async () => {
            const event = this.webhookEvent.value;
            const payload = JSON.parse(this.webhookPayload.value);
            let signature = '';

            if (this.webhookSignature) {
                signature = this.webhookSignature.value;
            }

            const loader = this.webhookSubmit.querySelector(this.loaderSelector);
            const badge = document.querySelector('.webhook-status');

            this.show(loader);
            this.disable(this.webhookSubmit);
            this.hide(badge);

            const result = await this.postJson(event, payload, signature);

            badge.textContent = result.status.toString(10);
            badge.classList.add(result.isOK ? 'bg-success' : 'bg-danger');
            badge.classList.remove(result.isOK ? 'bg-danger' : 'bg-success');

            this.show(badge);
            this.enable(this.webhookSubmit);
            this.hide(loader);
        };

        this.webhookSubmit.addEventListener('click', async () => await onSubmit());
    }

    private onApplicationLog(logEntry: LogEntry) {
        this.addLog(logEntry);
        console.log('Received log entry.', logEntry);
    }

    private onWebhook(webhookHeaders: any, webhookEvent: any) {
        this.addWebhook(webhookHeaders, webhookEvent);
        console.log('Received webhook.', webhookHeaders, webhookEvent);
    }

    private addWebhook(webhookHeaders: any, webhookEvent: any) {
        const delivery = webhookHeaders['X-GitHub-Delivery'];
        const event = webhookHeaders['X-GitHub-Event'];

        const indexId = `webhook-index-${delivery}`;
        const contentId = `webhook-content-${delivery}`;

        const eventItem = document.createElement('span');
        eventItem.classList.add('x-github-event');
        eventItem.textContent = event;

        const deliveryItem = document.createElement('span');
        deliveryItem.classList.add('x-github-delivery');
        deliveryItem.textContent = delivery;

        const indexItem = document.createElement('a');

        const indexPre = document.createElement('pre');
        indexPre.appendChild(eventItem);
        indexPre.append(' (');
        indexPre.appendChild(deliveryItem);
        indexPre.append(')');

        indexItem.appendChild(indexPre);

        const timestamp = moment();

        indexItem.classList.add('list-group-item');
        indexItem.classList.add('list-group-item-action');
        indexItem.classList.add('webhook-item');

        indexItem.setAttribute('id', indexId);
        indexItem.setAttribute('data-bs-toggle', 'list');
        indexItem.setAttribute('href', `#${contentId}`);
        indexItem.setAttribute('role', 'tab');
        indexItem.setAttribute('aria-controls', contentId);
        indexItem.setAttribute('aria-selected', 'false');
        indexItem.setAttribute('title', timestamp.toISOString());
        indexItem.setAttribute('x-github-event', event);
        indexItem.setAttribute('x-github-delivery', delivery);

        const contentItem = document.createElement('div');

        contentItem.classList.add('tab-pane');
        contentItem.classList.add('fade');

        contentItem.setAttribute('id', contentId);
        contentItem.setAttribute('role', 'tabpanel');
        contentItem.setAttribute('aria-labelledby', indexId);

        const pre = document.createElement('pre');

        const code = document.createElement('code');
        code.classList.add('webhook-content');
        code.classList.add('language-json');
        code.setAttribute('data-lang', 'json');
        code.setAttribute('x-github-event', event);
        code.setAttribute('x-github-delivery', delivery);

        let text = JSON.stringify(webhookHeaders, null, 2);
        text += '\n';
        text += JSON.stringify(webhookEvent, null, 2);

        code.innerText = text;

        pre.appendChild(code);
        contentItem.appendChild(pre);

        if (this.webhooksIndexContainer.firstChild) {
            this.webhooksIndexContainer.firstChild.before(indexItem);
        } else {
            this.webhooksIndexContainer.appendChild(indexItem);
        }

        this.webhooksContentContainer.appendChild(contentItem);
        this.webhooksCountContainer.innerText = `(${this.webhooksContentContainer.children.length.toLocaleString()})`;
    }

    private addLog(logEntry: LogEntry) {
        if (this.logsContainer.children.length === 0) {
            this.logsContainer.textContent = '';
        }

        const event = logEntry.eventName ?? logEntry.eventId;
        const timestamp = moment(logEntry.timestamp);

        const element = document.createElement('div');
        element.classList.add('log-entry');

        const timestampElement = document.createElement('span');
        timestampElement.classList.add('log-timestamp');
        timestampElement.innerText = timestamp.toISOString();
        timestampElement.title = logEntry.timestamp;

        const levelElement = document.createElement('span');
        levelElement.classList.add('log-level', `log-level-${logEntry.level.toLowerCase()}`);
        levelElement.innerText = logEntry.level;

        element.append(timestampElement, ' ', levelElement, ` ${logEntry.category}[${event}]: `);

        const message = logEntry.message;
        const match = message.match(/( )(([a-zA-Z0-9-]+)\/([a-zA-Z0-9-]+)#([0-9]+))( |.)/);

        if (match) {
            const whole = match[0];
            const leading = match[1];
            const text = match[2];
            const owner = match[3];
            const repo = match[4];
            const issue = match[5];
            const trailing = match[6];

            const prefix = message.substring(0, match.index);
            const suffix = message.substring(match.index + whole.length);

            const link = document.createElement('a');
            link.classList.add('log-link');
            link.href = `https://github.com/${owner}/${repo}/issues/${issue}`;
            link.innerText = text;
            link.target = '_blank';

            element.append(prefix, leading, link, trailing, suffix);
        } else {
            element.append(message);
        }

        if (logEntry.exception !== '') {
            const exception = document.createElement('div');
            exception.classList.add('log-exception');
            exception.innerText = logEntry.exception;
            element.append(exception);
        }

        this.logsContainer.appendChild(element);

        if (this.logsAutoscroll.checked) {
            this.logsContainer.scrollTop = this.logsContainer.scrollHeight;
        }
    }

    private disable(element: Element): void {
        element.setAttribute('disabled', '');
    }

    private enable(element: Element): void {
        element.removeAttribute('disabled');
    }

    private hide(element: Element) {
        element.classList.add(this.hidden);
    }

    private show(element: Element) {
        element.classList.remove(this.hidden);
    }

    private async postJson(event: string, payload: any, signature: string): Promise<WebhookResult> {
        const headers = new Headers();

        headers.set('Accept', 'application/json');
        headers.set('Content-Type', 'application/json');

        headers.set('X-GitHub-Delivery', `debug-${this.webhookDelivery++}`);
        headers.set('X-GitHub-Event', event);
        headers.set('X-GitHub-Hook-ID', 'debug');
        headers.set('X-GitHub-Hook-Installation-Target-ID', this.appId.value);
        headers.set('X-GitHub-Hook-Installation-Target-Type', 'integration');

        if (signature) {
            headers.set('X-Hub-Signature-256', signature);
        }

        const init = {
            method: 'POST',
            headers,
            body: JSON.stringify(payload),
        };

        const response = await fetch('/github-webhook', init);

        return {
            isOK: response.ok,
            status: response.status,
        };
    }
}

interface LogEntry {
    category: string;
    level: string;
    eventId: number;
    eventName?: string;
    exception?: string;
    message: string;
    timestamp: string;
}

interface WebhookResult {
    isOK: boolean;
    status: number;
}
