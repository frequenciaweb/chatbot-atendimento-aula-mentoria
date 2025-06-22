import { Component, OnInit, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../services/chat.service';
import { MensagemDto, ChatMensagemDto, ModeloIADto } from '../../models/mensagem.model';

interface EstadoConversa {
  primeiraInteracao: boolean;
  aguardandoConfirmacao: boolean;
  clienteIdentificado: boolean;
  nome?: string;
  telefone?: string;
  dadosTemporarios?: {
    nome: string;
    telefone: string;
  };
}

@Component({
  selector: 'app-chat',
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss'
})
export class ChatComponent implements OnInit {
  @ViewChild('chatContainer') chatContainer!: ElementRef;

  mensagens: ChatMensagemDto[] = [];
  novaMensagem: string = '';
  carregando: boolean = false;
  erro: string = '';
  
  modelosDisponiveis: ModeloIADto[] = [];
  modeloSelecionado: string = 'gpt-4o';
  
  estado: EstadoConversa = {
    primeiraInteracao: true,
    aguardandoConfirmacao: false,
    clienteIdentificado: false
  };

  constructor(private chatService: ChatService) {}

  ngOnInit() {
    this.carregarModelos();
    this.recuperarDadosClienteSalvos();
    if (this.mensagens.length === 0) {
      this.adicionarMensagemBoasVindas();
    }
  }

  carregarModelos() {
    this.chatService.buscarModelosDisponiveis().subscribe({
      next: (resposta) => {
        this.modelosDisponiveis = resposta.modelos;
        if (this.modelosDisponiveis.length > 0 && !this.modeloSelecionado) {
          this.modeloSelecionado = this.modelosDisponiveis[0].id;
        }
      },
      error: (erro) => {
        console.error('Erro ao carregar modelos:', erro);
        this.modelosDisponiveis = [];
        this.modeloSelecionado = '';
      }
    });
  }

  private recuperarDadosClienteSalvos() {
    const dadosSalvos = this.chatService.recuperarDadosCliente();
    if (dadosSalvos) {
      this.estado.clienteIdentificado = true;
      this.estado.nome = dadosSalvos.nome;
      this.estado.telefone = dadosSalvos.telefone;
      this.estado.primeiraInteracao = false;
      
      const mensagemRecuperada: ChatMensagemDto = {
        texto: `Bem-vindo de volta, ${dadosSalvos.nome}! Como posso te ajudar?`,
        origem: 'bot',
        dataCriacao: new Date()
      };
      this.mensagens.push(mensagemRecuperada);

    }
  }

  adicionarMensagemBoasVindas() {
    const mensagemBoasVindas: ChatMensagemDto = {
      texto: 'Olá! Bem-vindo ao nosso atendimento. Como posso ajudá-lo hoje?',
      origem: 'bot',
      dataCriacao: new Date()
    };
    this.mensagens.push(mensagemBoasVindas);
  }

  enviarMensagem() {
    if (!this.novaMensagem.trim() || this.carregando) {
      return;
    }

    const textoMensagem = this.novaMensagem.trim();
    this.novaMensagem = '';
    this.erro = '';
    this.carregando = true;

    const mensagemUsuario: ChatMensagemDto = {
      texto: textoMensagem,
      origem: 'cliente',
      dataCriacao: new Date()
    };
    this.mensagens.push(mensagemUsuario);
    this.scrollToBottom();

    const mensagemDto: MensagemDto = {
      texto: textoMensagem,
      modeloIA: this.modeloSelecionado || 'gpt-4o'
    };

    if (this.estado.aguardandoConfirmacao && this.isConfirmacaoPositiva(textoMensagem)) {
      if (this.estado.dadosTemporarios) {
        mensagemDto.nome = this.estado.dadosTemporarios.nome;
        mensagemDto.telefone = this.estado.dadosTemporarios.telefone;
      }
    } else if (this.estado.clienteIdentificado) {
      mensagemDto.nome = this.estado.nome;
      mensagemDto.telefone = this.estado.telefone;
    }

    this.chatService.enviarMensagem(mensagemDto).subscribe({
      next: (resposta) => {
        this.carregando = false;
        
        const mensagemBot: ChatMensagemDto = {
          texto: resposta.mensagem,
          origem: 'bot',
          dataCriacao: new Date()
        };
        this.mensagens.push(mensagemBot);

        this.estado.aguardandoConfirmacao = false;
        this.estado.dadosTemporarios = undefined;

        if (resposta.contaExcluida) {
          this.chatService.limparDadosCliente();
          this.resetarEstadoConversa(true);
          return;
        }

        if (resposta.dadosTemporarios) {
          this.estado.aguardandoConfirmacao = true;
          this.estado.dadosTemporarios = resposta.dadosTemporarios;
        }

        if (resposta.clienteIdentificado && resposta.nome && resposta.telefone) {
          this.estado.clienteIdentificado = true;
          this.estado.nome = resposta.nome;
          this.estado.telefone = resposta.telefone;
          this.chatService.salvarDadosCliente(resposta.nome, resposta.telefone);
        }

        if (resposta.historico && resposta.historico.length > 0) {
          this.carregarHistorico(resposta.historico);
        }

        this.scrollToBottom();
      },
      error: (erro) => {
        this.carregando = false;
        this.erro = erro;
        
        const mensagemErro: ChatMensagemDto = {
          texto: erro,
          origem: 'bot',
          dataCriacao: new Date()
        };
        this.mensagens.push(mensagemErro);
        this.scrollToBottom();
      }
    });
  }

  carregarHistorico(historico: ChatMensagemDto[]) {
    if (historico.length > 0) {
      this.mensagens = [this.mensagens[0], ...historico];
    }
  }

  onKeyPress(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.enviarMensagem();
    }
  }

  trocarModelo() {
  }

  private resetarEstadoConversa(aposExclusao: boolean = false) {
    this.estado = {
      primeiraInteracao: true,
      aguardandoConfirmacao: false,
      clienteIdentificado: false,
      nome: undefined,
      telefone: undefined
    };
    if (aposExclusao) {
      setTimeout(() => {
        this.mensagens = [];
        this.adicionarMensagemBoasVindas();
      }, 2000);
    }
  }

  private scrollToBottom() {
    setTimeout(() => {
      if (this.chatContainer) {
        this.chatContainer.nativeElement.scrollTop = this.chatContainer.nativeElement.scrollHeight;
      }
    }, 100);
  }

  getModeloNome(id: string): string {
    const modelo = this.modelosDisponiveis.find(m => m.id === id);
    return modelo ? modelo.nome : id;
  }

  getModeloTipo(id: string): string {
    const modelo = this.modelosDisponiveis.find(m => m.id === id);
    return modelo ? modelo.tipo : 'unknown';
  }

  private isConfirmacaoPositiva(texto: string): boolean {
    const textoLower = texto.toLowerCase().trim();
    return textoLower === 'sim' || textoLower === 'yes' || textoLower === 's';
  }
}
