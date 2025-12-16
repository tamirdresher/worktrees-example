import pika
import json
import psycopg2
import os
import time
from textblob import TextBlob
from datetime import datetime

class TaskConsumer:
    def __init__(self):
        self.is_running = False
        self.connection = None
        self.channel = None
        
        # RabbitMQ configuration
        self.rabbitmq_host = os.getenv('RABBITMQ_HOST', 'rabbitmq')
        self.queue_name = 'task_created'
        
        # PostgreSQL configuration
        self.db_config = {
            'host': os.getenv('POSTGRES_HOST', 'postgres'),
            'database': 'notetakerdb',
            'user': 'postgres',
            'password': 'postgres'
        }
    
    def connect_rabbitmq(self):
        """Connect to RabbitMQ with retry logic"""
        max_retries = 5
        retry_delay = 5
        
        # Check for Aspire connection string
        conn_str = os.getenv('ConnectionStrings__messaging')

        for attempt in range(max_retries):
            try:
                if conn_str:
                    parameters = pika.URLParameters(conn_str)
                else:
                    credentials = pika.PlainCredentials('guest', 'guest')
                    parameters = pika.ConnectionParameters(
                        host=self.rabbitmq_host,
                        credentials=credentials,
                        heartbeat=600,
                        blocked_connection_timeout=300
                    )
                
                self.connection = pika.BlockingConnection(parameters)
                self.channel = self.connection.channel()
                
                # Declare queue (idempotent)
                self.channel.queue_declare(queue=self.queue_name, durable=True)
                
                print(f"✓ Connected to RabbitMQ at {self.rabbitmq_host}")
                return True
            except Exception as e:
                print(f"✗ Failed to connect to RabbitMQ (attempt {attempt + 1}/{max_retries}): {e}")
                if attempt < max_retries - 1:
                    time.sleep(retry_delay)
        
        return False
    
    def analyze_task(self, title, description):
        """Analyze task using TextBlob for sentiment and simple keyword categorization"""
        # Combine title and description for analysis
        text = f"{title}. {description}"
        
        # Sentiment analysis
        blob = TextBlob(text)
        polarity = blob.sentiment.polarity
        
        if polarity > 0.1:
            sentiment = "positive"
        elif polarity < -0.1:
            sentiment = "negative"
        else:
            sentiment = "neutral"
        
        # Simple category detection based on keywords
        text_lower = text.lower()
        category = "general"
        
        work_keywords = ['work', 'meeting', 'project', 'deadline', 'report', 'email', 'client', 'business']
        personal_keywords = ['home', 'family', 'personal', 'buy', 'shopping', 'health', 'exercise', 'doctor']
        urgent_keywords = ['urgent', 'asap', 'important', 'critical', 'emergency', 'immediately', 'priority']
        
        if any(keyword in text_lower for keyword in urgent_keywords):
            category = "urgent"
        elif any(keyword in text_lower for keyword in work_keywords):
            category = "work"
        elif any(keyword in text_lower for keyword in personal_keywords):
            category = "personal"
        
        return category, sentiment
    
    def parse_dotnet_conn_str(self, conn_str):
        params = {}
        for part in conn_str.split(';'):
            if '=' in part:
                key, value = part.split('=', 1)
                key = key.strip().lower()
                if key == 'host': params['host'] = value
                elif key == 'database': params['database'] = value
                elif key == 'username' or key == 'user id': params['user'] = value
                elif key == 'password': params['password'] = value
                elif key == 'port': params['port'] = value
        return params

    def update_task_in_db(self, task_id, category, sentiment):
        """Update task with AI analysis results in PostgreSQL"""
        try:
            conn_str = os.getenv('ConnectionStrings__notetakerdb')
            if conn_str:
                db_params = self.parse_dotnet_conn_str(conn_str)
                conn = psycopg2.connect(**db_params)
            else:
                conn = psycopg2.connect(**self.db_config)

            cursor = conn.cursor()
            
            query = """
                UPDATE tasks 
                SET ai_category = %s, 
                    ai_sentiment = %s, 
                    ai_analyzed_at = %s 
                WHERE id = %s
            """
            
            cursor.execute(query, (category, sentiment, datetime.utcnow(), task_id))
            conn.commit()
            
            cursor.close()
            conn.close()
            
            print(f"✓ Updated task {task_id} with category={category}, sentiment={sentiment}")
            return True
        except Exception as e:
            print(f"✗ Error updating task in database: {e}")
            return False
    
    def process_message(self, ch, method, properties, body):
        """Process incoming message from RabbitMQ"""
        try:
            message = json.loads(body)
            task_id = message.get('task_id')
            title = message.get('title', '')
            description = message.get('description', '')
            
            print(f"→ Processing task: {task_id}")
            
            # Perform AI analysis
            category, sentiment = self.analyze_task(title, description)
            
            # Update database
            self.update_task_in_db(task_id, category, sentiment)
            
            # Acknowledge message
            ch.basic_ack(delivery_tag=method.delivery_tag)
            
        except Exception as e:
            print(f"✗ Error processing message: {e}")
            # Reject and requeue message on error
            ch.basic_nack(delivery_tag=method.delivery_tag, requeue=True)
    
    def start(self):
        """Start consuming messages from RabbitMQ"""
        print("Starting Task Consumer...")
        
        if not self.connect_rabbitmq():
            print("✗ Failed to start consumer - could not connect to RabbitMQ")
            return
        
        self.is_running = True
        
        # Set QoS to process one message at a time
        self.channel.basic_qos(prefetch_count=1)
        
        # Start consuming
        self.channel.basic_consume(
            queue=self.queue_name,
            on_message_callback=self.process_message
        )
        
        print(f"✓ Task Consumer is running. Waiting for messages from '{self.queue_name}' queue...")
        
        try:
            self.channel.start_consuming()
        except KeyboardInterrupt:
            self.stop()
        except Exception as e:
            print(f"✗ Consumer error: {e}")
            self.is_running = False
    
    def stop(self):
        """Stop the consumer"""
        self.is_running = False
        if self.channel:
            self.channel.stop_consuming()
        if self.connection:
            self.connection.close()
        print("✓ Task Consumer stopped")