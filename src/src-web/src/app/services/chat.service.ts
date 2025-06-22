import { Injectable, PLATFORM_ID, Inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { MensagemDto, RespostaDto, ModelosDisponiveis } from '../models/mensagem.model';

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private readonly baseUrl = environment.apiUrl;
  private readonly STORAGE_KEY = 'chatbot_cliente_dados';
  private isBrowser: boolean;

  constructor(
    private http: HttpClient,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  enviarMensagem(mensagem: MensagemDto): Observable<RespostaDto> {
    return this.http.post<RespostaDto>(`${this.baseUrl}/chat/enviar`, mensagem)
      .pipe(
        catchError(this.handleError)
      );
  }

  buscarModelosDisponiveis(): Observable<ModelosDisponiveis> {
    return this.http.get<ModelosDisponiveis>(`${this.baseUrl}/chat/modelos`)
      .pipe(
        catchError(this.handleError)
      );
  }

  // Métodos para gerenciar dados do cliente no localStorage
  salvarDadosCliente(nome: string, telefone: string): void {
    if (!this.isBrowser) return;
    const dados = { nome, telefone, timestamp: Date.now() };
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(dados));
  }

  recuperarDadosCliente(): { nome: string; telefone: string } | null {
    if (!this.isBrowser) return null;
    try {
      const dados = localStorage.getItem(this.STORAGE_KEY);
      if (!dados) return null;
      
      const clienteData = JSON.parse(dados);
      
      // Verificar se os dados não expiraram (24 horas)
      const agora = Date.now();
      const tempoExpiracao = 24 * 60 * 60 * 1000; // 24 horas em ms
      
      if (agora - clienteData.timestamp > tempoExpiracao) {
        this.limparDadosCliente();
        return null;
      }
      
      return { nome: clienteData.nome, telefone: clienteData.telefone };
    } catch (error) {
      console.error('Erro ao recuperar dados do cliente:', error);
      return null;
    }
  }

  limparDadosCliente(): void {
    if (!this.isBrowser) return;
    localStorage.removeItem(this.STORAGE_KEY);
  }

  private handleError(error: HttpErrorResponse) {
    let errorMessage = 'Desculpe, ocorreu um erro ao processar sua mensagem. Tente novamente.';
    
    if (error.status === 400) {
      // Erro 400 - verificar se é RespostaDto ou string
      if (error.error?.erro) {
        errorMessage = error.error.erro;
      } else if (typeof error.error === 'string') {
        errorMessage = error.error;
      } else {
        errorMessage = error.error?.message || 'Dados inválidos.';
      }
    } else if (error.status === 500) {
      // Erro 500 - mensagem padrão
      errorMessage = 'Desculpe, ocorreu um erro ao processar sua mensagem. Tente novamente.';
    } else if (error.status === 0) {
      // Erro de conexão
      errorMessage = 'Erro de conexão. Verifique sua internet e tente novamente.';
    }

    return throwError(() => errorMessage);
  }
}
